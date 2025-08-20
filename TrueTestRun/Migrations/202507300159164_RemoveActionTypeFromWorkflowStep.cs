namespace TrueTestRun.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class RemoveActionTypeFromWorkflowStep : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.WorkflowSteps", "ActionType");
        }
        
        public override void Down()
        {
            AddColumn("dbo.WorkflowSteps", "ActionType", c => c.Int(nullable: false));
        }
    }
}
