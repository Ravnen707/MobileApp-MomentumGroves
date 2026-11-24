BEGIN;
REVOKE USAGE ON SCHEMA momentum_groves FROM authenticated;
DROP FUNCTION IF EXISTS momentum_groves.mobile_training();
DROP FUNCTION IF EXISTS momentum_groves.mobile_save_weight(text, numeric, numeric);
DROP FUNCTION IF EXISTS momentum_groves.mobile_foods(bigint);
DROP FUNCTION IF EXISTS momentum_groves.mobile_save_food(bigint, text, uuid, jsonb);
DROP FUNCTION IF EXISTS momentum_groves.mobile_delete_food(bigint, text);
DROP FUNCTION IF EXISTS momentum_groves.mobile_training_document();
DROP FUNCTION IF EXISTS momentum_groves.mobile_require_account();
DROP POLICY IF EXISTS mobile_training_rows ON momentum_groves.appstate;
DROP POLICY IF EXISTS mobile_food_rows ON momentum_groves.customfoods;
DROP POLICY IF EXISTS mobile_recipe_links ON momentum_groves.recipeingredients;
DROP POLICY IF EXISTS mobile_import_rows ON momentum_groves.mobilefoodimports;
DO $cleanup$
BEGIN
    IF EXISTS (SELECT FROM pg_roles WHERE rolname = 'momentum_mobile_rpc') THEN
        REVOKE ALL ON SCHEMA momentum_groves FROM momentum_mobile_rpc;
        REVOKE ALL ON ALL TABLES IN SCHEMA momentum_groves FROM momentum_mobile_rpc;
        REVOKE ALL ON ALL SEQUENCES IN SCHEMA momentum_groves FROM momentum_mobile_rpc;
    END IF;
END
$cleanup$;
NOTIFY pgrst, 'reload schema';
COMMIT;
