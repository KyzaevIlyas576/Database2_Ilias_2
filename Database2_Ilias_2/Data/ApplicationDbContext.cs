using Database2_Ilias_2;
using Database2_Ilias_2.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;


namespace Database2_Ilias_2.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<FieldDefinition> FieldDefinitions => Set<FieldDefinition>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<FieldValue> FieldValues => Set<FieldValue>();
    public DbSet<DocumentVersion> DocumentVersions => Set<DocumentVersion>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);   // обязательно: таблицы Identity

        // --- Уникальность ---
        b.Entity<DocumentType>().HasIndex(t => t.Name).IsUnique();
        b.Entity<FieldDefinition>().HasIndex(f => new { f.DocumentTypeId, f.Name }).IsUnique();
        b.Entity<Tag>().HasIndex(t => t.Name).IsUnique();
        b.Entity<FieldValue>().HasIndex(v => new { v.DocumentId, v.FieldDefinitionId }).IsUnique();
        b.Entity<DocumentVersion>().HasIndex(v => new { v.DocumentId, v.VersionNumber }).IsUnique();

        // --- Индексы для поиска и фильтров ---
        b.Entity<Document>().HasIndex(d => d.Title);
        b.Entity<Document>().HasIndex(d => new { d.DocumentTypeId, d.Status });
        b.Entity<FieldValue>().HasIndex(v => new { v.FieldDefinitionId, v.ValueNumber });
        b.Entity<FieldValue>().HasIndex(v => new { v.FieldDefinitionId, v.ValueDate });
        b.Entity<AuditLog>().HasIndex(a => a.CreatedAt);

        // --- Поведение при удалении ---
        // Нельзя удалить тип, у которого есть документы
        b.Entity<Document>()
            .HasOne(d => d.DocumentType).WithMany(t => t.Documents)
            .HasForeignKey(d => d.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Нельзя удалить поле, по которому есть значения (его только отключают);
        // заодно избегаем ошибки SQL Server «multiple cascade paths»
        b.Entity<FieldValue>()
            .HasOne(v => v.FieldDefinition).WithMany(f => f.Values)
            .HasForeignKey(v => v.FieldDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Нельзя удалить рубрику с вложенными рубриками
        b.Entity<Category>()
            .HasOne(c => c.Parent).WithMany(c => c.Children)
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // При физическом удалении документа удаляются его значения и версии (каскад по умолчанию).
    }
}