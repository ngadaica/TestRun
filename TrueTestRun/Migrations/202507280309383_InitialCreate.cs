namespace TrueTestRun.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ApprovalSteps",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        DeptCode = c.String(maxLength: 50),
                        ADID = c.String(maxLength: 50),
                        Role = c.Int(nullable: false),
                        Timestamp = c.DateTime(),
                        Comment = c.String(maxLength: 4000),
                        RequestID = c.String(nullable: false, maxLength: 50),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Requests", t => t.RequestID, cascadeDelete: true)
                .Index(t => t.RequestID);
            
            CreateTable(
                "dbo.Requests",
                c => new
                    {
                        RequestID = c.String(nullable: false, maxLength: 50),
                        CreatedAt = c.DateTime(nullable: false),
                        CreatedByADID = c.String(maxLength: 50),
                        CurrentStepIndex = c.Int(nullable: false),
                        IsCompleted = c.Boolean(nullable: false),
                        IsRejected = c.Boolean(nullable: false),
                        CurrentPhase = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.RequestID);
            
            CreateTable(
                "dbo.RequestFields",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Key = c.String(nullable: false, maxLength: 255),
                        Value = c.String(maxLength: 4000),
                        RequestID = c.String(nullable: false, maxLength: 50),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Requests", t => t.RequestID, cascadeDelete: true)
                .Index(t => new { t.RequestID, t.Key }, name: "IX_RequestField_RequestID_Key");
            
            CreateTable(
                "dbo.WorkflowSteps",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Index = c.Int(nullable: false),
                        StepName = c.String(maxLength: 255),
                        Actor = c.Int(nullable: false),
                        ActionType = c.Int(nullable: false),
                        DeptCode = c.String(maxLength: 50),
                        Role = c.String(maxLength: 100),
                        NextApproverDept = c.String(maxLength: 50),
                        NextApproverRole = c.String(maxLength: 100),
                        Status = c.String(maxLength: 50),
                        ApproverADID = c.String(maxLength: 50),
                        ApprovedAt = c.DateTime(),
                        Comment = c.String(maxLength: 4000),
                        NextApproverADID = c.String(maxLength: 50),
                        RequestID = c.String(nullable: false, maxLength: 50),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Requests", t => t.RequestID, cascadeDelete: true)
                .Index(t => new { t.RequestID, t.Index }, name: "IX_WorkflowStep_RequestID_Index");
            
            CreateTable(
                "dbo.Users",
                c => new
                    {
                        ADID = c.String(nullable: false, maxLength: 50),
                        Name = c.String(maxLength: 255),
                        DeptCode = c.String(maxLength: 50),
                        Group = c.String(maxLength: 100),
                        Title = c.String(maxLength: 100),
                        Email = c.String(maxLength: 255),
                        Factory = c.String(maxLength: 100),
                        AvatarUrl = c.String(maxLength: 500),
                        Role = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.ADID);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.ApprovalSteps", "RequestID", "dbo.Requests");
            DropForeignKey("dbo.WorkflowSteps", "RequestID", "dbo.Requests");
            DropForeignKey("dbo.RequestFields", "RequestID", "dbo.Requests");
            DropIndex("dbo.WorkflowSteps", "IX_WorkflowStep_RequestID_Index");
            DropIndex("dbo.RequestFields", "IX_RequestField_RequestID_Key");
            DropIndex("dbo.ApprovalSteps", new[] { "RequestID" });
            DropTable("dbo.Users");
            DropTable("dbo.WorkflowSteps");
            DropTable("dbo.RequestFields");
            DropTable("dbo.Requests");
            DropTable("dbo.ApprovalSteps");
        }
    }
}
