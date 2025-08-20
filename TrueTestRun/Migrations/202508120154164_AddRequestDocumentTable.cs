namespace TrueTestRun.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddRequestDocumentTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.RequestDocuments",
                c => new
                    {
                        DocumentID = c.Int(nullable: false, identity: true),
                        RequestID = c.String(nullable: false, maxLength: 50),
                        FileName = c.String(nullable: false, maxLength: 255),
                        OriginalFileName = c.String(nullable: false, maxLength: 255),
                        ContentType = c.String(nullable: false, maxLength: 100),
                        FileSize = c.Long(nullable: false),
                        FilePath = c.String(nullable: false, maxLength: 500),
                        UploadedByADID = c.String(nullable: false, maxLength: 50),
                        UploadedAt = c.DateTime(nullable: false),
                        Description = c.String(maxLength: 1000),
                    })
                .PrimaryKey(t => t.DocumentID)
                .ForeignKey("dbo.Requests", t => t.RequestID, cascadeDelete: true)
                .Index(t => t.RequestID);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.RequestDocuments", "RequestID", "dbo.Requests");
            DropIndex("dbo.RequestDocuments", new[] { "RequestID" });
            DropTable("dbo.RequestDocuments");
        }
    }
}
