using System;
using Microsoft.EntityFrameworkCore;

namespace ReVersionVCS_API_Lambdas.Models
{
    public partial class ReVersion_DatabaseContext : DbContext
    {
        private const string DB_HOST_ENVIRONMENT_VARIABLE_LOOKUP = "RDS_DB_HOSTNAME";
        private const string DB_NAME_ENVIRONMENT_VARIABLE_LOOKUP = "RDS_DB_NAME";
        private const string DB_USERNAME_ENVIRONMENT_VARIABLE_LOOKUP = "RDS_DB_USERNAME";
        private const string DB_PASSWORD_ENVIRONMENT_VARIABLE_LOOKUP = "RDS_DB_PASSWORD";


        public ReVersion_DatabaseContext()
        {
        }

        public ReVersion_DatabaseContext(DbContextOptions<ReVersion_DatabaseContext> options)
            : base(options)
        {
        }

        public virtual DbSet<Branch> Branches { get; set; }
        public virtual DbSet<EventLog> EventLogs { get; set; }
        public virtual DbSet<PermissionRequest> PermissionRequests { get; set; }
        public virtual DbSet<Repository> Repositories { get; set; }
        public virtual DbSet<RepositoryPermission> RepositoryPermissions { get; set; }
        public virtual DbSet<User> Users { get; set; }
        public virtual DbSet<Version> Versions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string hostname = Environment.GetEnvironmentVariable(DB_HOST_ENVIRONMENT_VARIABLE_LOOKUP);
                string dbName = Environment.GetEnvironmentVariable(DB_NAME_ENVIRONMENT_VARIABLE_LOOKUP);
                string username = Environment.GetEnvironmentVariable(DB_USERNAME_ENVIRONMENT_VARIABLE_LOOKUP);
                string password = Environment.GetEnvironmentVariable(DB_PASSWORD_ENVIRONMENT_VARIABLE_LOOKUP);

                optionsBuilder.UseNpgsql($"Host='{hostname}';Database='{dbName}';Username='{username}';Password='{password}'");
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasAnnotation("ProductVersion", "2.2.4-servicing-10062");

            modelBuilder.Entity<Branch>(entity =>
            {
                entity.ToTable("branches");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('branches_id_seq'::regclass)");

                entity.Property(e => e.LatestFileHierarchy)
                    .IsRequired()
                    .HasColumnName("latest_file_hierarchy")
                    .HasColumnType("jsonb")
                    .HasDefaultValueSql("'{\"Name\": \"\", \"Type\": \"Directory\", \"Children\": []}'::jsonb");

                entity.Property(e => e.Locked)
                    .IsRequired()
                    .HasColumnName("locked")
                    .HasDefaultValueSql("false");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasColumnType("character varying");

                entity.Property(e => e.RepositoryId).HasColumnName("repository_id");

                entity.Property(e => e.VersionNumber)
                    .HasColumnName("version_number")
                    .HasDefaultValueSql("1");

                entity.HasOne(d => d.Repository)
                    .WithMany(p => p.Branches)
                    .HasForeignKey(d => d.RepositoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("branches_repository_id_fkey");
            });

            modelBuilder.Entity<EventLog>(entity =>
            {
                entity.ToTable("event_logs");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('event_log_id_seq'::regclass)");

                entity.Property(e => e.BranchId).HasColumnName("branch_id");

                entity.Property(e => e.LoggedAt)
                    .HasColumnName("logged_at")
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.Message)
                    .IsRequired()
                    .HasColumnName("message");

                entity.Property(e => e.Type)
                    .IsRequired()
                    .HasColumnName("type")
                    .HasColumnType("character varying");

                entity.Property(e => e.UserId).HasColumnName("user_id");

                entity.HasOne(d => d.Branch)
                    .WithMany(p => p.EventLogs)
                    .HasForeignKey(d => d.BranchId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("evlogfk");

                entity.HasOne(d => d.User)
                    .WithMany(p => p.EventLogs)
                    .HasForeignKey(d => d.UserId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("event_log_user_id_fkey");
            });

            modelBuilder.Entity<PermissionRequest>(entity =>
            {
                entity.ToTable("permission_requests");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('permission_requests_id_seq'::regclass)");

                entity.Property(e => e.EventId).HasColumnName("event_id");

                entity.Property(e => e.RepositoryId).HasColumnName("repository_id");

                entity.HasOne(d => d.Event)
                    .WithMany(p => p.PermissionRequests)
                    .HasForeignKey(d => d.EventId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("permreqfk");

                entity.HasOne(d => d.Repository)
                    .WithMany(p => p.PermissionRequests)
                    .HasForeignKey(d => d.RepositoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("permission_requests_repository_id_fkey");
            });

            modelBuilder.Entity<Repository>(entity =>
            {
                entity.ToTable("repositories");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('repositories_id_seq'::regclass)");

                entity.Property(e => e.Name)
                    .IsRequired()
                    .HasColumnName("name")
                    .HasColumnType("character varying");

                entity.Property(e => e.Owner).HasColumnName("owner");

                entity.HasOne(d => d.OwnerNavigation)
                    .WithMany(p => p.Repositories)
                    .HasForeignKey(d => d.Owner)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("repositories_owner_fk");
            });

            modelBuilder.Entity<RepositoryPermission>(entity =>
            {
                entity.ToTable("repository_permissions");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('repository_permissions_id_seq'::regclass)");

                entity.Property(e => e.PermittedUser).HasColumnName("permitted_user");

                entity.Property(e => e.RepositoryId).HasColumnName("repository_id");

                entity.HasOne(d => d.PermittedUserNavigation)
                    .WithMany(p => p.RepositoryPermissions)
                    .HasForeignKey(d => d.PermittedUser)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("repository_permissions_permitted_user_fkey");

                entity.HasOne(d => d.Repository)
                    .WithMany(p => p.RepositoryPermissions)
                    .HasForeignKey(d => d.RepositoryId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("repository_permissions_repository_id_fkey");
            });

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("users");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('users_id_seq'::regclass)");

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasColumnType("timestamp with time zone")
                    .HasDefaultValueSql("CURRENT_TIMESTAMP");

                entity.Property(e => e.UserName)
                    .IsRequired()
                    .HasColumnName("user_name")
                    .HasColumnType("character varying");
            });

            modelBuilder.Entity<Version>(entity =>
            {
                entity.ToTable("versions");

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasDefaultValueSql("nextval('versions_id_seq'::regclass)");

                entity.Property(e => e.BranchId).HasColumnName("branch_id");

                entity.Property(e => e.FileHierarchy)
                    .IsRequired()
                    .HasColumnName("file_hierarchy")
                    .HasColumnType("jsonb")
                    .HasDefaultValueSql("'{\"Name\": \"\", \"Type\": \"Directory\", \"Children\": []}'::jsonb");

                entity.Property(e => e.ParentBranch).HasColumnName("parent_branch");

                entity.Property(e => e.RollbackDelta)
                    .IsRequired()
                    .HasColumnName("rollback_delta");

                entity.Property(e => e.UpdateEventId).HasColumnName("update_event_id");

                entity.Property(e => e.VersionNumber).HasColumnName("version_number");

                entity.HasOne(d => d.Branch)
                    .WithMany(p => p.VersionsBranch)
                    .HasForeignKey(d => d.BranchId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("versions_branch_id_fkey");

                entity.HasOne(d => d.ParentBranchNavigation)
                    .WithMany(p => p.VersionsParentBranchNavigation)
                    .HasForeignKey(d => d.ParentBranch)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("versions_parent_branch_fkey");

                entity.HasOne(d => d.UpdateEvent)
                    .WithMany(p => p.Versions)
                    .HasForeignKey(d => d.UpdateEventId)
                    .OnDelete(DeleteBehavior.ClientSetNull)
                    .HasConstraintName("versions_update_event_id_fkey");
            });

            modelBuilder.HasSequence<int>("branches_id_seq");

            modelBuilder.HasSequence<int>("event_log_id_seq");

            modelBuilder.HasSequence<int>("permission_requests_id_seq");

            modelBuilder.HasSequence<int>("repositories_id_seq");

            modelBuilder.HasSequence<int>("users_id_seq");

            modelBuilder.HasSequence<int>("versions_id_seq");
        }
    }
}
