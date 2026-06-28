-- Удаление серверного хранения SSH-учётных данных.
-- После этого логин и пароль хранятся локально в settings.json.

ALTER TABLE IF EXISTS pm02.servers
    DROP CONSTRAINT IF EXISTS servers_credential_id_fkey;

ALTER TABLE IF EXISTS pm02.servers
    DROP COLUMN IF EXISTS credential_id;

DROP TABLE IF EXISTS pm02.user_credentials;
