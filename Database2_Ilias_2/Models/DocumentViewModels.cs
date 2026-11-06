using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

// Если у вас другое корневое пространство имён, замените Database2_Ilias_2.
namespace Database2_Ilias_2.Models;

// Одно поле формы документа. Описание поля (подпись, тип, варианты) берётся из БД,
// из формы приходят только FieldDefinitionId и Value.
public class FieldInput
{
    public int FieldDefinitionId { get; set; }

    [BindNever, ValidateNever] public string Label { get; set; } = "";
    [BindNever] public FieldDataType DataType { get; set; }
    [BindNever] public bool IsRequired { get; set; }
    [BindNever, ValidateNever] public List<string> Options { get; set; } = new();

    public string? Value { get; set; }   // из формы всё приходит строкой
}

public class DocumentFormModel
{
    public int Id { get; set; }                   // 0 = новый документ
    public int DocumentTypeId { get; set; }

    [BindNever, ValidateNever] public string TypeName { get; set; } = "";

    [Required(ErrorMessage = "Введите заголовок")]
    [StringLength(300, ErrorMessage = "Не более 300 символов")]
    [Display(Name = "Заголовок")]
    public string Title { get; set; } = "";

    [StringLength(2000, ErrorMessage = "Не более 2000 символов")]
    [Display(Name = "Краткое описание")]
    public string? Summary { get; set; }

    [Display(Name = "Статус")]
    public DocumentStatus Status { get; set; } = DocumentStatus.Draft;

    public List<FieldInput> Fields { get; set; } = new();
}

public class DocumentListItem
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string TypeName { get; set; } = "";
    public DocumentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? AuthorEmail { get; set; }
}

public class DocumentListViewModel
{
    public List<DocumentListItem> Items { get; set; } = new();
    public List<DocumentType> Types { get; set; } = new();

    public string? Query { get; set; }
    public int? TypeId { get; set; }
    public DocumentStatus? Status { get; set; }

    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
}

public class FieldDisplay
{
    public string Label { get; set; } = "";
    public string? Value { get; set; }
}

public class DocumentDetailsViewModel
{
    public Document Document { get; set; } = null!;
    public List<FieldDisplay> Fields { get; set; } = new();
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public static class DocumentStatusExtensions
{
    public static string Ru(this DocumentStatus s) => s switch
    {
        DocumentStatus.Draft => "Черновик",
        DocumentStatus.Active => "Действующий",
        DocumentStatus.Archived => "Архивный",
        _ => s.ToString()
    };
}