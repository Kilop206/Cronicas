START TRANSACTION;
DROP TABLE integration_outbox;

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261104172257_IntegrationOutbox';

COMMIT;
