START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109121911_CharacterSystemVersions') THEN
    ALTER TABLE characters ADD "SystemVersionId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109121911_CharacterSystemVersions') THEN
    CREATE TABLE rpg_system_versions (
        "Id" uuid NOT NULL,
        "RpgSystemId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" character varying(2000) NOT NULL,
        "DefinitionJson" jsonb NOT NULL,
        "ContentHash" character varying(64) NOT NULL,
        "SourceRevision" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_rpg_system_versions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_rpg_system_versions_rpg_systems_RpgSystemId" FOREIGN KEY ("RpgSystemId") REFERENCES rpg_systems ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109121911_CharacterSystemVersions') THEN
    CREATE INDEX "IX_characters_SystemVersionId" ON characters ("SystemVersionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109121911_CharacterSystemVersions') THEN
    CREATE UNIQUE INDEX "IX_rpg_system_versions_RpgSystemId_ContentHash" ON rpg_system_versions ("RpgSystemId", "ContentHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109121911_CharacterSystemVersions') THEN
    ALTER TABLE characters ADD CONSTRAINT "FK_characters_rpg_system_versions_SystemVersionId" FOREIGN KEY ("SystemVersionId") REFERENCES rpg_system_versions ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109121911_CharacterSystemVersions') THEN
    CREATE FUNCTION cronicas_immutable_system_version() RETURNS trigger LANGUAGE plpgsql AS $$
    BEGIN
        RAISE EXCEPTION 'Captured RPG system versions are immutable.' USING ERRCODE = 'P0001';
    END;
    $$;
    CREATE TRIGGER immutable_system_version BEFORE UPDATE OR DELETE ON rpg_system_versions
    FOR EACH ROW EXECUTE FUNCTION cronicas_immutable_system_version();
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261109121911_CharacterSystemVersions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261109121911_CharacterSystemVersions', '10.0.13');
    END IF;
END $EF$;
COMMIT;
