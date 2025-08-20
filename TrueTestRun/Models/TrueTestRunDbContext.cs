using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace TrueTestRun.Models
{
    public class TrueTestRunDbContext : DbContext
    {
        public DbSet<Request> Requests { get; set; }
        public DbSet<RequestField> RequestFields { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<WorkflowStep> WorkflowSteps { get; set; }
        public DbSet<ApprovalStep> ApprovalSteps { get; set; }
        public DbSet<RequestDocument> RequestDocuments { get; set; }

        public TrueTestRunDbContext() : base("name=TrueTestRun")
        {
            Database.SetInitializer(new CreateDatabaseIfNotExists<TrueTestRunDbContext>());
        }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            // Request - RequestField (1:nhiều)
            modelBuilder.Entity<RequestField>()
                .HasRequired(rf => rf.Request)
                .WithMany(r => r.Fields)
                .HasForeignKey(rf => rf.RequestID)
                .WillCascadeOnDelete(true);

            // Request - WorkflowStep (1:nhiều cho History)
            modelBuilder.Entity<WorkflowStep>()
                .HasRequired(ws => ws.Request)
                .WithMany(r => r.History)
                .HasForeignKey(ws => ws.RequestID)
                .WillCascadeOnDelete(true);

            // Request - ApprovalStep (1:nhiều)
            modelBuilder.Entity<ApprovalStep>()
                .HasRequired(a => a.Request)
                .WithMany()
                .HasForeignKey(a => a.RequestID)
                .WillCascadeOnDelete(true);

            // Tạo index cho performance
            modelBuilder.Entity<RequestField>()
                .HasIndex(rf => new { rf.RequestID, rf.Key })
                .HasName("IX_RequestField_RequestID_Key");

            modelBuilder.Entity<WorkflowStep>()
                .HasIndex(ws => new { ws.RequestID, ws.Index })
                .HasName("IX_WorkflowStep_RequestID_Index");

            // THÊM: Cấu hình cho RequestDocument
            modelBuilder.Entity<RequestDocument>()
                .HasRequired(d => d.Request)
                .WithMany()
                .HasForeignKey(d => d.RequestID)
                .WillCascadeOnDelete(true);

            base.OnModelCreating(modelBuilder);
        }
    }
}