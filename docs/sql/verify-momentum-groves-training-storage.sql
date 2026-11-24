-- Run in the SQL Editor after Storage setup. No personal data or Auth UUIDs are selected.
SELECT id, public, file_size_limit, allowed_mime_types
FROM storage.buckets WHERE id = 'momentum-training-images';

SELECT policyname, permissive, roles, cmd
FROM pg_policies
WHERE schemaname = 'storage' AND tablename = 'objects'
    AND policyname IN (
        'momentum_training_images_owner', 'momentum_training_images_read_guard',
        'momentum_training_images_insert_guard', 'momentum_training_images_update_guard',
        'momentum_training_images_delete_guard')
ORDER BY policyname;
