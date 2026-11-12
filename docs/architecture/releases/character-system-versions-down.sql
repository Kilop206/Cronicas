START TRANSACTION;
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM rpg_system_versions) OR EXISTS (SELECT 1 FROM characters WHERE "SystemVersionId" IS NOT NULL) THEN
        RAISE EXCEPTION 'Cannot remove captured system versions. Preserve the additive schema and roll back the application only.' USING ERRCODE = 'P0001';
    END IF;
END $$;
DROP TRIGGER immutable_system_version ON rpg_system_versions;
DROP FUNCTION cronicas_immutable_system_version();

ALTER TABLE characters DROP CONSTRAINT "FK_characters_rpg_system_versions_SystemVersionId";

DROP TABLE rpg_system_versions;

DROP INDEX "IX_characters_SystemVersionId";

ALTER TABLE characters DROP COLUMN "SystemVersionId";

DELETE FROM "__EFMigrationsHistory"
WHERE "MigrationId" = '20261109121911_CharacterSystemVersions';

COMMIT;
