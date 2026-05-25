-- ============================================================================
-- SiteForce Smart Payment Disbursement — Audit Table Immutability
-- ============================================================================
-- This script enforces append-only behavior on the AuditEvents table.
-- Run AFTER EF Core migrations create the table.
-- Creates a dedicated app role that CANNOT update or delete audit records.
-- ============================================================================

-- Create app role if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'app_role')
BEGIN
    CREATE ROLE app_role;
END
GO

-- Grant general permissions
GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO app_role;
GO

-- DENY update and delete specifically on AuditEvents
DENY UPDATE ON dbo.AuditEvents TO app_role;
DENY DELETE ON dbo.AuditEvents TO app_role;
GO

-- Also create a trigger as defense-in-depth (blocks even sa from accidental mutations)
IF OBJECT_ID('dbo.TR_AuditEvents_PreventMutation', 'TR') IS NOT NULL
    DROP TRIGGER dbo.TR_AuditEvents_PreventMutation;
GO

CREATE TRIGGER dbo.TR_AuditEvents_PreventMutation
ON dbo.AuditEvents
INSTEAD OF UPDATE, DELETE
AS
BEGIN
    RAISERROR('AuditEvents table is append-only. UPDATE and DELETE operations are not permitted.', 16, 1);
    ROLLBACK TRANSACTION;
END
GO

PRINT 'Audit immutability enforced: DENY + trigger on AuditEvents table.';
GO
