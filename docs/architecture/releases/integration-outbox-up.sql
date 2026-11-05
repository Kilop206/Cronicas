START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104172257_IntegrationOutbox') THEN
    CREATE TABLE integration_outbox (
        "Id" uuid NOT NULL,
        "Payload" jsonb NOT NULL,
        "OccurredAt" timestamp with time zone NOT NULL,
        "AvailableAt" timestamp with time zone NOT NULL,
        "ProcessedAt" timestamp with time zone,
        "Attempts" integer NOT NULL,
        CONSTRAINT "PK_integration_outbox" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104172257_IntegrationOutbox') THEN
    CREATE INDEX "IX_integration_outbox_AvailableAt_OccurredAt" ON integration_outbox ("AvailableAt", "OccurredAt") WHERE "ProcessedAt" IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104172257_IntegrationOutbox') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261104172257_IntegrationOutbox', '10.0.13');
    END IF;
END $EF$;
COMMIT;
