-- Create a PRIVATE momentum-training-images bucket in the Dashboard first.
-- Replace the zero UUID in this copy with the same Auth UUID used for mobile data.
BEGIN;
SELECT set_config('momentum_groves.storage_account', '00000000-0000-0000-0000-000000000000', true);

DO $setup$
DECLARE
    account uuid := current_setting('momentum_groves.storage_account')::uuid;
    bucket text := 'momentum-training-images';
    owner_check text;
BEGIN
    IF account = '00000000-0000-0000-0000-000000000000' OR NOT EXISTS
        (SELECT FROM auth.users WHERE id = account AND NOT coalesce(is_anonymous, false)
            AND email_confirmed_at IS NOT NULL) THEN
        RAISE EXCEPTION 'Set the UUID of a confirmed Supabase Auth user';
    END IF;
    IF NOT EXISTS (
        SELECT FROM storage.buckets WHERE id = bucket AND NOT public
            AND file_size_limit BETWEEN 8000000 AND 8388608
            AND allowed_mime_types @> ARRAY['image/jpeg','image/png','image/webp']
            AND allowed_mime_types <@ ARRAY['image/jpeg','image/png','image/webp']
    ) THEN
        RAISE EXCEPTION 'Create the private bucket with an 8 MB limit and the documented MIME types';
    END IF;
    IF NOT EXISTS (SELECT FROM pg_class WHERE oid = 'storage.objects'::regclass AND relrowsecurity) THEN
        RAISE EXCEPTION 'RLS must be enabled on storage.objects';
    END IF;

    owner_check := format(
        '((SELECT auth.uid()) = %L::uuid AND NOT coalesce((SELECT (auth.jwt()->>''is_anonymous'')::boolean), false))',
        account::text);
    DROP POLICY IF EXISTS momentum_training_images_owner ON storage.objects;
    DROP POLICY IF EXISTS momentum_training_images_read_guard ON storage.objects;
    DROP POLICY IF EXISTS momentum_training_images_insert_guard ON storage.objects;
    DROP POLICY IF EXISTS momentum_training_images_update_guard ON storage.objects;
    DROP POLICY IF EXISTS momentum_training_images_delete_guard ON storage.objects;

    EXECUTE format('CREATE POLICY momentum_training_images_owner ON storage.objects
        FOR SELECT TO authenticated USING (bucket_id = %L AND %s)', bucket, owner_check);
    -- Restrictive policies also protect this bucket from broad existing policies.
    EXECUTE format('CREATE POLICY momentum_training_images_read_guard ON storage.objects
        AS RESTRICTIVE FOR SELECT TO PUBLIC USING (bucket_id <> %L OR %s)', bucket, owner_check);
    EXECUTE format('CREATE POLICY momentum_training_images_insert_guard ON storage.objects
        AS RESTRICTIVE FOR INSERT TO PUBLIC WITH CHECK (bucket_id <> %L)', bucket);
    EXECUTE format('CREATE POLICY momentum_training_images_update_guard ON storage.objects
        AS RESTRICTIVE FOR UPDATE TO PUBLIC USING (bucket_id <> %L) WITH CHECK (bucket_id <> %L)', bucket, bucket);
    EXECUTE format('CREATE POLICY momentum_training_images_delete_guard ON storage.objects
        AS RESTRICTIVE FOR DELETE TO PUBLIC USING (bucket_id <> %L)', bucket);
END
$setup$;
COMMIT;
