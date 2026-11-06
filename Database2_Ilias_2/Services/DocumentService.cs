using System.Globalization;
using Database2_Ilias_2.Data;
using Database2_Ilias_2.Models;
using Microsoft.EntityFrameworkCore;

// Если у вас другое корневое пространство имён, замените Database2_Ilias_2.
namespace Database2_Ilias_2.Services;

public enum DeleteResult { Ok, NotFound, Forbidden }

// Вся бизнес-логика работы с документами. Контроллер только вызывает эти методы.
public class DocumentService
{
    private const string DateFormat = "yyyy-MM-dd";   // формат <input type="date">

    private readonly ApplicationDbContext _db;

    public DocumentService(ApplicationDbContext db) => _db = db;

    // ================= Список и просмотр =================

    public Task<List<DocumentType>> GetActiveTypesAsync() =>
        _db.DocumentTypes.Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync();

    public async Task<DocumentListViewModel> ListAsync(string? q, int? typeId, DocumentStatus? status, int page)
    {
        const int pageSize = 10;

        var query = _db.Documents.Where(d => !d.IsDeleted);

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(d => d.Title.Contains(term) || (d.Summary != null && d.Summary.Contains(term)));
        }
        if (typeId != null) query = query.Where(d => d.DocumentTypeId == typeId.Value);
        if (status != null) query = query.Where(d => d.Status == status.Value);

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query
            .OrderByDescending(d => d.CreatedAt).ThenByDescending(d => d.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(d => new DocumentListItem
            {
                Id = d.Id,
                Title = d.Title,
                TypeName = d.DocumentType.Name,
                Status = d.Status,
                CreatedAt = d.CreatedAt,
                AuthorEmail = d.CreatedBy != null ? d.CreatedBy.Email : null
            })
            .ToListAsync();

        return new DocumentListViewModel
        {
            Items = items,
            Types = await _db.DocumentTypes.OrderBy(t => t.Name).ToListAsync(),
            Query = q,
            TypeId = typeId,
            Status = status,
            Page = page,
            TotalPages = totalPages
        };
    }

    public async Task<DocumentDetailsViewModel?> GetDetailsAsync(int id)
    {
        var doc = await _db.Documents
            .Include(d => d.DocumentType)
            .Include(d => d.CreatedBy)
            .Include(d => d.FieldValues).ThenInclude(v => v.FieldDefinition)
            .FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
        if (doc == null) return null;

        var fields = doc.FieldValues
            .OrderBy(v => v.FieldDefinition.SortOrder).ThenBy(v => v.FieldDefinition.Id)
            .Select(v => new FieldDisplay
            {
                Label = v.FieldDefinition.Label,
                Value = ToDisplay(v, v.FieldDefinition.DataType)
            })
            .ToList();

        return new DocumentDetailsViewModel { Document = doc, Fields = fields };
    }

    // ================= Построение формы по описанию полей =================

    public async Task<DocumentFormModel?> BuildNewAsync(int typeId)
    {
        var type = await _db.DocumentTypes.FirstOrDefaultAsync(t => t.Id == typeId && t.IsActive);
        if (type == null) return null;

        return new DocumentFormModel
        {
            DocumentTypeId = type.Id,
            TypeName = type.Name,
            Fields = await BuildInputsAsync(type.Id, stored: null, posted: null)
        };
    }

    public async Task<DocumentFormModel?> BuildEditAsync(int documentId)
    {
        var doc = await _db.Documents
            .Include(d => d.DocumentType)
            .Include(d => d.FieldValues)
            .FirstOrDefaultAsync(d => d.Id == documentId && !d.IsDeleted);
        if (doc == null) return null;

        var stored = doc.FieldValues.ToDictionary(v => v.FieldDefinitionId);

        return new DocumentFormModel
        {
            Id = doc.Id,
            DocumentTypeId = doc.DocumentTypeId,
            TypeName = doc.DocumentType.Name,
            Title = doc.Title,
            Summary = doc.Summary,
            Status = doc.Status,
            Fields = await BuildInputsAsync(doc.DocumentTypeId, stored, posted: null)
        };
    }

    // После отправки формы: описание полей снова берём из БД (форме не доверяем),
    // а введённые значения подставляем по FieldDefinitionId.
    public async Task<DocumentFormModel?> RebindAsync(DocumentFormModel posted)
    {
        if (posted.Id != 0)
        {
            // Редактирование: тип документа определяет БД, а не скрытое поле формы
            var typeId = await _db.Documents
                .Where(d => d.Id == posted.Id && !d.IsDeleted)
                .Select(d => (int?)d.DocumentTypeId)
                .FirstOrDefaultAsync();
            if (typeId == null) return null;
            posted.DocumentTypeId = typeId.Value;
        }

        var type = await _db.DocumentTypes.FirstOrDefaultAsync(t => t.Id == posted.DocumentTypeId);
        if (type == null) return null;
        if (posted.Id == 0 && !type.IsActive) return null;   // в архивном типе новые документы не создаются

        var values = posted.Fields
            .GroupBy(f => f.FieldDefinitionId)
            .ToDictionary(g => g.Key, g => g.First().Value);

        posted.TypeName = type.Name;
        posted.Fields = await BuildInputsAsync(type.Id, stored: null, posted: values);
        return posted;
    }

    private async Task<List<FieldInput>> BuildInputsAsync(
        int typeId, Dictionary<int, FieldValue>? stored, Dictionary<int, string?>? posted)
    {
        var defs = await _db.FieldDefinitions
            .Where(f => f.DocumentTypeId == typeId && f.IsActive)
            .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
            .ToListAsync();

        var inputs = new List<FieldInput>();
        foreach (var f in defs)
        {
            string? value = null;
            if (posted != null)
                posted.TryGetValue(f.Id, out value);
            else if (stored != null && stored.TryGetValue(f.Id, out var sv))
                value = ToText(sv, f.DataType);

            inputs.Add(new FieldInput
            {
                FieldDefinitionId = f.Id,
                Label = f.Label,
                DataType = f.DataType,
                IsRequired = f.IsRequired,
                Options = (f.Options ?? "")
                    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList(),
                Value = value
            });
        }
        return inputs;
    }

    // ================= Проверка введённых значений =================

    // Возвращает ошибки в виде «ключ поля формы -> сообщение».
    public Dictionary<string, string> Validate(DocumentFormModel model)
    {
        var errors = new Dictionary<string, string>();

        for (var i = 0; i < model.Fields.Count; i++)
        {
            var f = model.Fields[i];
            var key = $"Fields[{i}].Value";
            var raw = f.Value?.Trim();

            if (string.IsNullOrEmpty(raw))
            {
                if (f.IsRequired) errors[key] = $"Поле «{f.Label}» обязательно";
                continue;
            }

            switch (f.DataType)
            {
                case FieldDataType.Text:
                    if (raw.Length > 4000) errors[key] = "Не более 4000 символов";
                    break;

                case FieldDataType.Number:
                    if (!TryParseNumber(raw, out var number)) errors[key] = "Введите число";
                    else if (Math.Abs(number) >= 100_000_000_000_000m) errors[key] = "Слишком большое число";
                    break;

                case FieldDataType.Date:
                    if (!TryParseDate(raw, out _)) errors[key] = "Введите корректную дату";
                    break;

                case FieldDataType.Boolean:
                    if (raw != "true" && raw != "false") errors[key] = "Выберите «Да» или «Нет»";
                    break;

                case FieldDataType.List:
                    if (!f.Options.Contains(raw)) errors[key] = "Выберите значение из списка";
                    break;
            }
        }
        return errors;
    }

    // ================= Сохранение =================

    public async Task<int> CreateAsync(DocumentFormModel m, string? userId)
    {
        var doc = new Document
        {
            DocumentTypeId = m.DocumentTypeId,
            Title = m.Title.Trim(),
            Summary = string.IsNullOrWhiteSpace(m.Summary) ? null : m.Summary.Trim(),
            Status = m.Status,
            CreatedByUserId = userId
        };

        foreach (var f in m.Fields.Where(f => !string.IsNullOrWhiteSpace(f.Value)))
        {
            var v = new FieldValue { FieldDefinitionId = f.FieldDefinitionId };
            Fill(v, f);
            doc.FieldValues.Add(v);
        }

        // Документ, его значения и запись журнала сохраняются одной транзакцией
        await using var tx = await _db.Database.BeginTransactionAsync();

        _db.Documents.Add(doc);
        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "Create",
            EntityType = nameof(Document),
            EntityId = doc.Id,
            Details = doc.Title
        });
        await _db.SaveChangesAsync();

        await tx.CommitAsync();
        return doc.Id;
    }

    public async Task<bool> UpdateAsync(DocumentFormModel m, string? userId)
    {
        var doc = await _db.Documents
            .Include(d => d.FieldValues)
            .FirstOrDefaultAsync(d => d.Id == m.Id && !d.IsDeleted);
        if (doc == null) return false;

        doc.Title = m.Title.Trim();
        doc.Summary = string.IsNullOrWhiteSpace(m.Summary) ? null : m.Summary.Trim();
        doc.Status = m.Status;

        // Меняем только значения активных полей; значения отключённых полей остаются как есть
        foreach (var f in m.Fields)
        {
            var existing = doc.FieldValues.FirstOrDefault(v => v.FieldDefinitionId == f.FieldDefinitionId);

            if (string.IsNullOrWhiteSpace(f.Value))
            {
                if (existing != null) _db.FieldValues.Remove(existing);
                continue;
            }

            if (existing == null)
            {
                existing = new FieldValue { FieldDefinitionId = f.FieldDefinitionId };
                doc.FieldValues.Add(existing);
            }
            Fill(existing, f);
        }

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "Update",
            EntityType = nameof(Document),
            EntityId = doc.Id,
            Details = doc.Title
        });

        await _db.SaveChangesAsync();   // один вызов: всё или ничего
        return true;
    }

    // Логическое удаление (в корзину). Редактор удаляет только свои документы, админ любые.
    public async Task<DeleteResult> SoftDeleteAsync(int id, string? userId, bool isAdmin)
    {
        var doc = await _db.Documents.FirstOrDefaultAsync(d => d.Id == id && !d.IsDeleted);
        if (doc == null) return DeleteResult.NotFound;
        if (!isAdmin && doc.CreatedByUserId != userId) return DeleteResult.Forbidden;

        doc.IsDeleted = true;
        doc.DeletedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            Action = "Delete",
            EntityType = nameof(Document),
            EntityId = doc.Id,
            Details = doc.Title
        });
        await _db.SaveChangesAsync();
        return DeleteResult.Ok;
    }

    // ================= Преобразования значений =================

    // Значение поля -> в нужную колонку FieldValue (одна колонка, остальные пустые)
    private static void Fill(FieldValue v, FieldInput f)
    {
        v.ValueText = null;
        v.ValueNumber = null;
        v.ValueDate = null;
        v.ValueBool = null;

        var raw = f.Value!.Trim();
        switch (f.DataType)
        {
            case FieldDataType.Number:
                TryParseNumber(raw, out var n);
                v.ValueNumber = n;
                break;
            case FieldDataType.Date:
                TryParseDate(raw, out var d);
                v.ValueDate = d;
                break;
            case FieldDataType.Boolean:
                v.ValueBool = raw == "true";
                break;
            default:   // Text и List хранятся как текст
                v.ValueText = raw;
                break;
        }
    }

    // Значение из БД -> строка для поля формы (инвариантный формат: <input type="number"/"date">)
    private static string? ToText(FieldValue v, FieldDataType type) => type switch
    {
        FieldDataType.Number => v.ValueNumber?.ToString("0.####", CultureInfo.InvariantCulture),
        FieldDataType.Date => v.ValueDate?.ToString(DateFormat, CultureInfo.InvariantCulture),
        FieldDataType.Boolean => v.ValueBool == null ? null : (v.ValueBool.Value ? "true" : "false"),
        _ => v.ValueText
    };

    // Значение из БД -> строка для показа человеку
    private static string? ToDisplay(FieldValue v, FieldDataType type) => type switch
    {
        FieldDataType.Number => v.ValueNumber?.ToString("0.####", CultureInfo.CurrentCulture),
        FieldDataType.Date => v.ValueDate?.ToString("dd.MM.yyyy"),
        FieldDataType.Boolean => v.ValueBool == null ? null : (v.ValueBool.Value ? "Да" : "Нет"),
        _ => v.ValueText
    };

    private static bool TryParseNumber(string s, out decimal value) =>
        decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                         CultureInfo.InvariantCulture, out value);

    private static bool TryParseDate(string s, out DateTime value) =>
        DateTime.TryParseExact(s, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out value);
}