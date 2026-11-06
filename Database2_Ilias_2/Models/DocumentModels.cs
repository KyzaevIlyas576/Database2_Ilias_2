using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Замените DocumentSystem на корневое пространство имён вашего проекта.
namespace Database2_Ilias_2;

// ---------- Перечисления ----------

public enum FieldDataType { Text = 0, Number = 1, Date = 2, Boolean = 3, List = 4 }

public enum DocumentStatus { Draft = 0, Active = 1, Archived = 2 }

// ---------- Описание структуры (метаданные) ----------

// Тип документа: «Письмо», «Протокол», «Договор»...
public class DocumentType
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = "";

    [StringLength(500)]
    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public List<FieldDefinition> Fields { get; set; } = new();
    public List<Document> Documents { get; set; } = new();
}

// Поле типа документа: «Номер договора» (текст), «Сумма» (число) и т. д.
public class FieldDefinition
{
    public int Id { get; set; }

    public int DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = null!;

    [Required, StringLength(100)]
    public string Name { get; set; } = "";          // служебное имя, уникально внутри типа

    [Required, StringLength(200)]
    public string Label { get; set; } = "";         // подпись в форме

    public FieldDataType DataType { get; set; } = FieldDataType.Text;
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }

    [StringLength(2000)]
    public string? Options { get; set; }            // варианты для списка, через «;»

    public bool IsActive { get; set; } = true;      // поле не удаляется, а отключается

    public List<FieldValue> Values { get; set; } = new();
}

// ---------- Классификация ----------

public class Category
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = "";

    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public List<Category> Children { get; set; } = new();

    public List<Document> Documents { get; set; } = new();
}

public class Tag
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = "";

    // Связь «многие ко многим»: EF сам создаст таблицу DocumentTag
    public List<Document> Documents { get; set; } = new();
}

// ---------- Документы ----------

public class Document
{
    public int Id { get; set; }

    public int DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = null!;

    [Required, StringLength(300)]
    public string Title { get; set; } = "";

    [StringLength(2000)]
    public string? Summary { get; set; }

    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public IdentityUser? CreatedBy { get; set; }

    public bool IsDeleted { get; set; }             // логическое удаление («корзина»)
    public DateTime? DeletedAt { get; set; }

    public List<FieldValue> FieldValues { get; set; } = new();
    public List<DocumentVersion> Versions { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
}

// Значение одного поля у одного документа (модель EAV).
// Заполняется ровно одна из колонок Value*, в зависимости от FieldDefinition.DataType.
public class FieldValue
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public int FieldDefinitionId { get; set; }
    public FieldDefinition FieldDefinition { get; set; } = null!;

    [StringLength(4000)] public string? ValueText { get; set; }     // текст и выбор из списка
    [Precision(18, 4)] public decimal? ValueNumber { get; set; }
    public DateTime? ValueDate { get; set; }
    public bool? ValueBool { get; set; }
}

// Версия содержимого документа (файл и/или текст).
public class DocumentVersion
{
    public int Id { get; set; }

    public int DocumentId { get; set; }
    public Document Document { get; set; } = null!;

    public int VersionNumber { get; set; }          // 1, 2, 3... внутри документа

    [StringLength(260)] public string? FileName { get; set; }       // исходное имя файла
    [StringLength(128)] public string? ContentType { get; set; }
    public long Size { get; set; }
    [StringLength(64)] public string? Sha256 { get; set; }
    [StringLength(500)] public string? StoragePath { get; set; }    // сгенерированный путь на диске

    public string? TextContent { get; set; }        // текст документа или извлечённый текст (для поиска)

    [StringLength(500)] public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public IdentityUser? CreatedBy { get; set; }
}

// ---------- Журнал ----------

public class AuditLog
{
    public int Id { get; set; }

    public string? UserId { get; set; }             // без внешнего ключа: журнал переживает удаление пользователя

    [Required, StringLength(50)]
    public string Action { get; set; } = "";        // Create, Update, Download, Delete, Login...

    [StringLength(50)]
    public string? EntityType { get; set; }         // Document, DocumentType...
    public int? EntityId { get; set; }

    [StringLength(2000)]
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// ---------- Роли ----------

public static class Roles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Reader = "Reader";
}