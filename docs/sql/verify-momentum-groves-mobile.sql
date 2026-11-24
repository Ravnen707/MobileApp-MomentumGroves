-- Read-only PostgreSQL permission checks. No personal rows are selected.
SELECT role_name,
    has_schema_privilege(role_name, 'momentum_groves', 'USAGE') AS schema_usage,
    has_function_privilege(role_name, 'momentum_groves.mobile_training()', 'EXECUTE') AS training_rpc,
    has_function_privilege(role_name, 'momentum_groves.mobile_save_food(bigint,text,uuid,jsonb)', 'EXECUTE') AS food_write_rpc
FROM (VALUES ('anon'), ('authenticated')) AS roles(role_name);

SELECT c.relname, c.relrowsecurity AS rls_enabled,
    has_table_privilege('anon', c.oid, 'SELECT,INSERT,UPDATE,DELETE') AS anon_table_access,
    has_table_privilege('authenticated', c.oid, 'SELECT,INSERT,UPDATE,DELETE') AS authenticated_table_access,
    has_any_column_privilege('authenticated', c.oid, 'SELECT,INSERT,UPDATE') AS authenticated_column_access
FROM pg_class AS c
JOIN pg_namespace AS n ON n.oid = c.relnamespace
WHERE n.nspname = 'momentum_groves' AND c.relkind = 'r'
ORDER BY c.relname;

SELECT p.proname, owner.rolname AS owner, owner.rolcanlogin, owner.rolbypassrls,
    has_function_privilege('anon', p.oid, 'EXECUTE') AS anon_execute,
    has_function_privilege('authenticated', p.oid, 'EXECUTE') AS authenticated_execute
FROM pg_proc AS p
JOIN pg_namespace AS n ON n.oid = p.pronamespace
JOIN pg_roles AS owner ON owner.oid = p.proowner
WHERE n.nspname = 'momentum_groves'
ORDER BY p.proname;

SELECT policyname, tablename, roles, cmd
FROM pg_policies WHERE schemaname = 'momentum_groves' ORDER BY tablename, policyname;
