-- Run as the database administrator after deploying the web opt-in.
-- Replace the zero UUID with the UUID from Authentication > Users.
BEGIN;
SET LOCAL search_path = pg_catalog, momentum_groves;
SELECT set_config('momentum_groves.mobile_account', '00000000-0000-0000-0000-000000000000', true);

DO $setup$
DECLARE
    account uuid := current_setting('momentum_groves.mobile_account')::uuid;
BEGIN
    IF account = '00000000-0000-0000-0000-000000000000' OR NOT EXISTS
        (SELECT FROM auth.users WHERE id = account AND NOT coalesce(is_anonymous, false)) THEN
        RAISE EXCEPTION 'Set the UUID of an existing non-anonymous Supabase Auth user';
    END IF;
    IF NOT EXISTS (SELECT FROM pg_roles WHERE rolname = 'momentum_mobile_rpc') THEN
        CREATE ROLE momentum_mobile_rpc NOLOGIN NOINHERIT NOBYPASSRLS;
    END IF;
    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'momentum_mobile_rpc'
        AND (rolcanlogin OR rolbypassrls OR rolsuper OR rolinherit)) THEN
        RAISE EXCEPTION 'The existing momentum_mobile_rpc role has unsafe attributes';
    END IF;
    -- Role creators retain administration. Allow the installer to manage the RPCs later.
    IF current_setting('server_version_num')::int >= 160000 THEN
        EXECUTE format('GRANT momentum_mobile_rpc TO %I WITH INHERIT TRUE, SET TRUE', current_user);
    ELSE
        EXECUTE format('GRANT momentum_mobile_rpc TO %I', current_user);
    END IF;
    EXECUTE format($definition$
        CREATE OR REPLACE FUNCTION momentum_groves.mobile_require_account()
        RETURNS void LANGUAGE plpgsql STABLE SET search_path = pg_catalog AS $body$
        DECLARE
            claims jsonb := nullif(current_setting('request.jwt.claims', true), '')::jsonb;
        BEGIN
            IF (claims->>'sub') IS DISTINCT FROM %L
                OR (claims->>'role') IS DISTINCT FROM 'authenticated'
                OR coalesce((claims->>'is_anonymous')::boolean, false) THEN
                RAISE EXCEPTION 'Account is not authorized' USING ERRCODE = '42501';
            END IF;
        END;
        $body$
    $definition$, account::text);
END
$setup$;

REVOKE ALL ON SCHEMA momentum_groves FROM PUBLIC, anon, authenticated, service_role, authenticator;
REVOKE ALL ON ALL TABLES IN SCHEMA momentum_groves FROM PUBLIC, anon, authenticated, service_role, authenticator;
REVOKE ALL ON ALL SEQUENCES IN SCHEMA momentum_groves FROM PUBLIC, anon, authenticated, service_role, authenticator;
GRANT USAGE ON SCHEMA momentum_groves TO authenticated;
GRANT USAGE, CREATE ON SCHEMA momentum_groves TO momentum_mobile_rpc;

ALTER TABLE appstate ENABLE ROW LEVEL SECURITY;
ALTER TABLE customfoods ENABLE ROW LEVEL SECURITY;
ALTER TABLE recipes ENABLE ROW LEVEL SECURITY;
ALTER TABLE recipeingredients ENABLE ROW LEVEL SECURITY;
ALTER TABLE mobilefoodimports ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS mobile_training_rows ON appstate;
CREATE POLICY mobile_training_rows ON appstate TO momentum_mobile_rpc
    USING (key IN ('sunhed.training-programs.v3', 'sunhed.training-programs.v2'))
    WITH CHECK (key = 'sunhed.training-programs.v3');
DROP POLICY IF EXISTS mobile_food_rows ON customfoods;
CREATE POLICY mobile_food_rows ON customfoods TO momentum_mobile_rpc USING (true);
DROP POLICY IF EXISTS mobile_recipe_links ON recipeingredients;
CREATE POLICY mobile_recipe_links ON recipeingredients FOR SELECT TO momentum_mobile_rpc USING (true);
DROP POLICY IF EXISTS mobile_import_rows ON mobilefoodimports;
CREATE POLICY mobile_import_rows ON mobilefoodimports TO momentum_mobile_rpc USING (true);

GRANT SELECT, INSERT, UPDATE ON appstate TO momentum_mobile_rpc;
GRANT SELECT, INSERT, UPDATE, DELETE ON customfoods TO momentum_mobile_rpc;
GRANT SELECT, INSERT, DELETE ON mobilefoodimports TO momentum_mobile_rpc;
GRANT SELECT ON recipeingredients TO momentum_mobile_rpc;
DO $sequence$
BEGIN
    EXECUTE format('GRANT USAGE ON SEQUENCE %s TO momentum_mobile_rpc',
        pg_get_serial_sequence('momentum_groves.customfoods', 'id'));
END
$sequence$;

-- Return the web app's normalised document while retaining all other JSON properties.
CREATE OR REPLACE FUNCTION momentum_groves.mobile_training_document()
RETURNS jsonb LANGUAGE plpgsql STABLE SET search_path = pg_catalog, momentum_groves AS $$
DECLARE
    document jsonb;
    groups jsonb := '[]';
    legacy_group record;
    item record;
BEGIN
    SELECT jsonvalue::jsonb INTO document FROM appstate
    WHERE key IN ('sunhed.training-programs.v3', 'sunhed.training-programs.v2')
    ORDER BY key DESC LIMIT 1;

    IF jsonb_typeof(document->'groups') IS DISTINCT FROM 'array' THEN
        FOR legacy_group IN SELECT * FROM (VALUES
            ('full', 'My programmes'), ('custom', 'My other programmes')) AS names(id, name)
        LOOP
            IF jsonb_typeof(document->legacy_group.id) = 'array' THEN
                IF jsonb_array_length(document->legacy_group.id) > 0 THEN
                    groups := groups || jsonb_build_array(jsonb_build_object('id', legacy_group.id,
                        'name', legacy_group.name, 'programmes', document->legacy_group.id));
                END IF;
            END IF;
        END LOOP;
        document := jsonb_build_object('groups', groups,
            'dashboardGroupId', CASE WHEN groups->0->>'id' = 'full' THEN 'full' END);
    END IF;

    FOR item IN
        SELECT ARRAY['groups', (g.pos - 1)::text, 'programmes', (p.pos - 1)::text,
            'exercises', (e.pos - 1)::text, 'id'] AS path,
            format('legacy:%s:%s:%s', g.value->>'id', p.value->>'id', e.pos - 1) AS id
        FROM jsonb_array_elements(document->'groups') WITH ORDINALITY AS g(value, pos)
        CROSS JOIN LATERAL jsonb_array_elements(CASE WHEN jsonb_typeof(g.value->'programmes') = 'array'
            THEN g.value->'programmes' ELSE '[]'::jsonb END) WITH ORDINALITY AS p(value, pos)
        CROSS JOIN LATERAL jsonb_array_elements(CASE WHEN jsonb_typeof(p.value->'exercises') = 'array'
            THEN p.value->'exercises' ELSE '[]'::jsonb END) WITH ORDINALITY AS e(value, pos)
        WHERE jsonb_typeof(e.value) = 'object' AND coalesce(btrim(e.value->>'id'), '') = ''
    LOOP
        document := jsonb_set(document, item.path, to_jsonb(item.id));
    END LOOP;
    RETURN document;
END
$$;

CREATE OR REPLACE FUNCTION momentum_groves.mobile_training()
RETURNS jsonb LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = pg_catalog, momentum_groves AS $$
BEGIN
    PERFORM mobile_require_account();
    RETURN mobile_training_document();
END
$$;

CREATE OR REPLACE FUNCTION momentum_groves.mobile_save_weight(exercise_id text, expected_kg numeric, requested_kg numeric)
RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, momentum_groves AS $$
DECLARE
    document jsonb;
    target record;
    stored numeric;
BEGIN
    PERFORM mobile_require_account();
    IF coalesce(btrim(exercise_id), '') = '' OR length(exercise_id) > 300
        OR expected_kg IS NULL OR requested_kg IS NULL OR requested_kg < 0 OR requested_kg > 79228162514264337593543950335
        OR expected_kg IN ('NaN'::numeric, 'Infinity'::numeric, '-Infinity'::numeric) THEN
        RETURN jsonb_build_object('status', 'invalid');
    END IF;

    LOCK TABLE appstate IN SHARE ROW EXCLUSIVE MODE;
    document := mobile_training_document();
    SELECT ARRAY['groups', (g.pos - 1)::text, 'programmes', (p.pos - 1)::text,
        'exercises', (e.pos - 1)::text, 'kg'] AS path, e.value AS exercise INTO target
    FROM jsonb_array_elements(document->'groups') WITH ORDINALITY AS g(value, pos)
    CROSS JOIN LATERAL jsonb_array_elements(CASE WHEN jsonb_typeof(g.value->'programmes') = 'array'
        THEN g.value->'programmes' ELSE '[]'::jsonb END) WITH ORDINALITY AS p(value, pos)
    CROSS JOIN LATERAL jsonb_array_elements(CASE WHEN jsonb_typeof(p.value->'exercises') = 'array'
        THEN p.value->'exercises' ELSE '[]'::jsonb END) WITH ORDINALITY AS e(value, pos)
    WHERE e.value->>'id' = exercise_id
    ORDER BY g.pos, p.pos, e.pos LIMIT 1;

    IF NOT FOUND OR target.exercise->>'type' = 'cardio' THEN
        RETURN jsonb_build_object('status', 'missing');
    END IF;
    BEGIN
        stored := coalesce((target.exercise->>'kg')::numeric, 0);
    EXCEPTION WHEN invalid_text_representation OR numeric_value_out_of_range THEN
        stored := 0;
    END;

    IF stored = requested_kg THEN
        RETURN jsonb_build_object('status', 'saved', 'kg', stored);
    END IF;
    IF stored <> expected_kg THEN
        RETURN jsonb_build_object('status', 'conflict', 'kg', stored);
    END IF;

    INSERT INTO appstate (key, jsonvalue, updatedutc)
    VALUES ('sunhed.training-programs.v3', jsonb_set(document, target.path, to_jsonb(requested_kg))::text,
        to_char(clock_timestamp() AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.MS"Z"'))
    ON CONFLICT (key) DO UPDATE SET jsonvalue = excluded.jsonvalue, updatedutc = excluded.updatedutc;
    RETURN jsonb_build_object('status', 'saved', 'kg', requested_kg);
END
$$;

CREATE OR REPLACE FUNCTION momentum_groves.mobile_foods(after_id bigint DEFAULT NULL)
RETURNS jsonb LANGUAGE plpgsql STABLE SECURITY DEFINER SET search_path = pg_catalog, momentum_groves AS $$
BEGIN
    PERFORM mobile_require_account();
    RETURN (SELECT coalesce(jsonb_agg(to_jsonb(page) ORDER BY page.id), '[]'::jsonb)
        FROM (SELECT id, name, unit, kcal, protein, carbs, fat, fiber, md5(to_jsonb(f)::text) AS version
            FROM customfoods AS f WHERE after_id IS NULL OR id > after_id ORDER BY id LIMIT 100) AS page);
END
$$;

CREATE OR REPLACE FUNCTION momentum_groves.mobile_save_food(food_id bigint DEFAULT NULL, expected_version text DEFAULT NULL,
    request_id uuid DEFAULT NULL, food jsonb DEFAULT NULL)
RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, momentum_groves AS $$
DECLARE
    incoming customfoods%ROWTYPE;
    saved_id bigint;
    current_version text;
    values_match boolean;
    nutrient double precision;
BEGIN
    PERFORM mobile_require_account();
    BEGIN
        incoming := jsonb_populate_record(NULL::customfoods, food);
    EXCEPTION WHEN data_exception THEN
        RETURN jsonb_build_object('status', 'invalid');
    END;
    incoming.name := btrim(incoming.name);
    IF coalesce(length(incoming.name), 0) NOT BETWEEN 1 AND 200 OR incoming.unit IS NULL
        OR incoming.unit NOT IN ('g', 'ml') OR request_id IS NULL THEN
        RETURN jsonb_build_object('status', 'invalid');
    END IF;
    FOREACH nutrient IN ARRAY ARRAY[incoming.kcal, incoming.protein, incoming.carbs,
        incoming.fat, coalesce(incoming.fiber, 0)] LOOP
        IF nutrient IS NULL OR nutrient < 0 OR nutrient >= 'Infinity'::double precision THEN
            RETURN jsonb_build_object('status', 'invalid');
        END IF;
    END LOOP;

    LOCK TABLE customfoods, mobilefoodimports IN SHARE ROW EXCLUSIVE MODE;
    IF food_id IS NULL THEN
        SELECT foodid INTO saved_id FROM mobilefoodimports WHERE mobileid = request_id::text;
    ELSE
        saved_id := food_id;
    END IF;
    IF saved_id IS NOT NULL THEN
        SELECT md5(to_jsonb(f)::text),
            (f.name, f.unit, f.kcal, f.protein, f.carbs, f.fat, f.fiber)
                IS NOT DISTINCT FROM (incoming.name, incoming.unit, incoming.kcal, incoming.protein,
                    incoming.carbs, incoming.fat, incoming.fiber)
        INTO current_version, values_match FROM customfoods AS f WHERE f.id = saved_id;
        -- A repeated create or update is safe if the requested values are already stored.
        IF values_match THEN
            RETURN jsonb_build_object('status', 'saved', 'id', saved_id);
        END IF;
        IF NOT FOUND OR food_id IS NULL OR current_version IS DISTINCT FROM expected_version THEN
            RETURN jsonb_build_object('status', 'conflict');
        END IF;
    END IF;

    IF EXISTS (SELECT FROM customfoods WHERE lower(btrim(name)) = lower(incoming.name)
        AND id IS DISTINCT FROM food_id)
        AND NOT EXISTS (SELECT FROM customfoods WHERE id = food_id
            AND lower(btrim(name)) = lower(incoming.name)) THEN
        RETURN jsonb_build_object('status', 'duplicate');
    END IF;
    IF food_id IS NULL THEN
        INSERT INTO customfoods (name, unit, kcal, protein, carbs, fat, fiber)
        VALUES (incoming.name, incoming.unit, incoming.kcal, incoming.protein, incoming.carbs, incoming.fat,
            incoming.fiber) RETURNING id INTO saved_id;
        INSERT INTO mobilefoodimports (mobileid, foodid) VALUES (request_id::text, saved_id);
    ELSE
        UPDATE customfoods SET name = incoming.name, unit = incoming.unit, kcal = incoming.kcal,
            protein = incoming.protein, carbs = incoming.carbs, fat = incoming.fat, fiber = incoming.fiber
        WHERE id = food_id;
        saved_id := food_id;
    END IF;
    RETURN jsonb_build_object('status', 'saved', 'id', saved_id);
END
$$;

CREATE OR REPLACE FUNCTION momentum_groves.mobile_delete_food(food_id bigint, expected_version text)
RETURNS jsonb LANGUAGE plpgsql SECURITY DEFINER SET search_path = pg_catalog, momentum_groves AS $$
DECLARE
    current_version text;
BEGIN
    PERFORM mobile_require_account();
    LOCK TABLE customfoods, mobilefoodimports IN SHARE ROW EXCLUSIVE MODE;
    SELECT md5(to_jsonb(f)::text) INTO current_version FROM customfoods AS f WHERE f.id = food_id;
    IF NOT FOUND THEN
        RETURN jsonb_build_object('status', 'saved');
    END IF;
    IF current_version IS DISTINCT FROM expected_version THEN
        RETURN jsonb_build_object('status', 'conflict');
    END IF;
    IF EXISTS (SELECT FROM recipeingredients WHERE foodid = food_id) THEN
        RETURN jsonb_build_object('status', 'in_use');
    END IF;
    BEGIN
        DELETE FROM mobilefoodimports WHERE foodid = food_id;
        DELETE FROM customfoods WHERE id = food_id;
    EXCEPTION WHEN foreign_key_violation THEN
        RETURN jsonb_build_object('status', 'in_use');
    END;
    RETURN jsonb_build_object('status', 'saved');
END
$$;

DO $permissions$
DECLARE
    signature text;
BEGIN
    FOREACH signature IN ARRAY ARRAY[
        'mobile_require_account()', 'mobile_training_document()', 'mobile_training()',
        'mobile_save_weight(text,numeric,numeric)', 'mobile_foods(bigint)',
        'mobile_save_food(bigint,text,uuid,jsonb)', 'mobile_delete_food(bigint,text)'
    ] LOOP
        EXECUTE 'ALTER FUNCTION momentum_groves.' || signature || ' OWNER TO momentum_mobile_rpc';
        EXECUTE 'REVOKE ALL ON FUNCTION momentum_groves.' || signature ||
            ' FROM PUBLIC, anon, authenticated, service_role, authenticator';
    END LOOP;
END
$permissions$;
GRANT EXECUTE ON FUNCTION mobile_training(), mobile_save_weight(text,numeric,numeric),
    mobile_foods(bigint), mobile_save_food(bigint,text,uuid,jsonb), mobile_delete_food(bigint,text)
    TO authenticated;
REVOKE CREATE ON SCHEMA momentum_groves FROM momentum_mobile_rpc;
DO $verify$
DECLARE
    api_role name;
BEGIN
    FOREACH api_role IN ARRAY ARRAY['anon', 'authenticated', 'authenticator', 'service_role']::name[] LOOP
        IF pg_has_role(api_role, 'momentum_mobile_rpc', 'MEMBER') THEN
            RAISE EXCEPTION 'Data API role % must not be an RPC owner role member', api_role;
        END IF;
    END LOOP;
    IF EXISTS (SELECT FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'momentum_groves' AND c.relkind IN ('r', 'v', 'm', 'p', 'f')
            AND has_any_column_privilege('authenticated', c.oid, 'SELECT,INSERT,UPDATE,REFERENCES')) THEN
        RAISE EXCEPTION 'Direct access remains on a relation in the mobile schema';
    END IF;
    IF EXISTS (SELECT FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
        WHERE n.nspname = 'momentum_groves' AND has_function_privilege('authenticated', p.oid, 'EXECUTE')
            AND p.oid NOT IN ('momentum_groves.mobile_training()'::regprocedure,
                'momentum_groves.mobile_save_weight(text,numeric,numeric)'::regprocedure,
                'momentum_groves.mobile_foods(bigint)'::regprocedure,
                'momentum_groves.mobile_save_food(bigint,text,uuid,jsonb)'::regprocedure,
                'momentum_groves.mobile_delete_food(bigint,text)'::regprocedure)) THEN
        RAISE EXCEPTION 'An unrelated routine is executable by the mobile API role';
    END IF;
END
$verify$;
NOTIFY pgrst, 'reload schema';
COMMIT;
