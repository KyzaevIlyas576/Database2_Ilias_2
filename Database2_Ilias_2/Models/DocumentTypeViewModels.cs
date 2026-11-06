using System.ComponentModel.DataAnnotations;

// Если у вас другое корневое пространство имён, замените Database2_Ilias_2.
namespace Database2_Ilias_2.Models;

// Модели форм: в них только то, что пользователь вводит (а не вся сущность БД).

public class TypeFormModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Введите название")]
    [StringLength(100, ErrorMessage = "Не более 100 символов")]
    [Display(Name = "Название")]
    public string Name { get; set; } = "";

    [StringLength(500, ErrorMessage = "Не более 500 символов")]
    [Display(Name = "Описание")]
    public string? Description { get; set; }
}

public class FieldFormModel
{
    public int Id { get; set; }
    public int DocumentTypeId { get; set; }

    [Required(ErrorMessage = "Введите служебное имя")]
    [StringLength(100)]
    [RegularExpression("^[A-Za-z0-9_]+$", ErrorMessage = "Только латинские буквы, цифры и знак _")]
    [Display(Name = "Служебное имя")]
    public string Name { get; set; } = "";

    [Required(ErrorMessage = "Введите подпись")]
    [StringLength(200)]
    [Display(Name = "Подпись в форме")]
    public string Label { get; set; } = "";

    [Display(Name = "Тип данных")]
    public FieldDataType DataType { get; set; } = FieldDataType.Text;

    [Display(Name = "Обязательное")]
    public bool IsRequired { get; set; }

    [Display(Name = "Порядок")]
    public int SortOrder { get; set; }

    [StringLength(2000)]
    [Display(Name = "Варианты списка (через «;»)")]
    public string? Options { get; set; }
}

public class TypeListItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public int FieldCount { get; set; }
    public int DocumentCount { get; set; }
}

public class TypeDetailsViewModel
{
    public DocumentType Type { get; set; } = null!;
    public FieldFormModel NewField { get; set; } = new();
}

public static class FieldDataTypeExtensions
{
    // Русское название типа данных для интерфейса
    public static string Ru(this FieldDataType t) => t switch
    {
        FieldDataType.Text => "Текст",
        FieldDataType.Number => "Число",
        FieldDataType.Date => "Дата",
        FieldDataType.Boolean => "Да / нет",
        FieldDataType.List => "Выбор из списка",
        _ => t.ToString()
    };
}