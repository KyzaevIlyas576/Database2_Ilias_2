using System.Security.Claims;
using Database2_Ilias_2.Data;
using Database2_Ilias_2.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

// Если у вас другое корневое пространство имён, замените Database2_Ilias_2.
namespace Database2_Ilias_2.Controllers;

[Authorize(Roles = Roles.Admin)]
public class DocumentTypesController : Controller
{
    private readonly ApplicationDbContext _db;

    public DocumentTypesController(ApplicationDbContext db) => _db = db;

    // ================= Типы документов =================

    public async Task<IActionResult> Index()
    {
        var items = await _db.DocumentTypes
            .OrderBy(t => t.Name)
            .Select(t => new TypeListItem
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                IsActive = t.IsActive,
                FieldCount = t.Fields.Count(f => f.IsActive),
                DocumentCount = t.Documents.Count()
            })
            .ToListAsync();

        return View(items);
    }

    public Task<IActionResult> Details(int id) => ShowDetails(id, null);

    public IActionResult Create() => View("Form", new TypeFormModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TypeFormModel model)
    {
        var name = model.Name.Trim();
        if (await _db.DocumentTypes.AnyAsync(t => t.Name == name))
            ModelState.AddModelError(nameof(model.Name), "Тип с таким названием уже существует");
        if (!ModelState.IsValid) return View("Form", model);

        var type = new DocumentType { Name = name, Description = model.Description };
        _db.DocumentTypes.Add(type);
        await _db.SaveChangesAsync();
        await AuditAsync("Create", nameof(DocumentType), type.Id, type.Name);

        TempData["Message"] = "Тип создан. Теперь добавьте поля.";
        return RedirectToAction(nameof(Details), new { id = type.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var type = await _db.DocumentTypes.FindAsync(id);
        if (type == null) return NotFound();

        return View("Form", new TypeFormModel { Id = type.Id, Name = type.Name, Description = type.Description });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TypeFormModel model)
    {
        var type = await _db.DocumentTypes.FindAsync(model.Id);
        if (type == null) return NotFound();

        var name = model.Name.Trim();
        if (await _db.DocumentTypes.AnyAsync(t => t.Name == name && t.Id != model.Id))
            ModelState.AddModelError(nameof(model.Name), "Тип с таким названием уже существует");
        if (!ModelState.IsValid) return View("Form", model);

        type.Name = name;
        type.Description = model.Description;
        await _db.SaveChangesAsync();
        await AuditAsync("Update", nameof(DocumentType), type.Id, type.Name);

        TempData["Message"] = "Изменения сохранены.";
        return RedirectToAction(nameof(Details), new { id = type.Id });
    }

    // Типы не удаляются физически: их отправляют в архив и возвращают обратно
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var type = await _db.DocumentTypes.FindAsync(id);
        if (type == null) return NotFound();

        type.IsActive = !type.IsActive;
        await _db.SaveChangesAsync();
        await AuditAsync(type.IsActive ? "Activate" : "Archive", nameof(DocumentType), id, type.Name);

        TempData["Message"] = type.IsActive ? "Тип снова активен." : "Тип отправлен в архив.";
        return RedirectToAction(nameof(Index));
    }

    // ================= Поля типа =================

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddField(FieldFormModel model)
    {
        var type = await _db.DocumentTypes.FindAsync(model.DocumentTypeId);
        if (type == null) return NotFound();

        await ValidateFieldAsync(model);
        if (!ModelState.IsValid) return await ShowDetails(model.DocumentTypeId, model);

        var field = new FieldDefinition
        {
            DocumentTypeId = model.DocumentTypeId,
            Name = model.Name.Trim(),
            Label = model.Label.Trim(),
            DataType = model.DataType,
            IsRequired = model.IsRequired,
            SortOrder = model.SortOrder,
            Options = model.DataType == FieldDataType.List ? string.Join(";", ParseOptions(model.Options)) : null
        };
        _db.FieldDefinitions.Add(field);
        await _db.SaveChangesAsync();
        await AuditAsync("Create", nameof(FieldDefinition), field.Id, $"{type.Name}: {field.Name}");

        TempData["Message"] = "Поле добавлено.";
        return RedirectToAction(nameof(Details), new { id = type.Id });
    }

    public async Task<IActionResult> EditField(int id)
    {
        var field = await _db.FieldDefinitions.FindAsync(id);
        if (field == null) return NotFound();

        return View(new FieldFormModel
        {
            Id = field.Id,
            DocumentTypeId = field.DocumentTypeId,
            Name = field.Name,
            Label = field.Label,
            DataType = field.DataType,
            IsRequired = field.IsRequired,
            SortOrder = field.SortOrder,
            Options = field.Options
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EditField(FieldFormModel model)
    {
        var field = await _db.FieldDefinitions.FindAsync(model.Id);
        if (field == null) return NotFound();

        model.DocumentTypeId = field.DocumentTypeId;   // скрытому полю формы не доверяем

        await ValidateFieldAsync(model);

        // Правило целостности: тип данных нельзя менять, если по полю уже есть значения
        if (model.DataType != field.DataType &&
            await _db.FieldValues.AnyAsync(v => v.FieldDefinitionId == field.Id))
        {
            ModelState.AddModelError(nameof(model.DataType),
                "Нельзя менять тип данных: по этому полю уже есть значения");
        }
        if (!ModelState.IsValid) return View(model);

        field.Name = model.Name.Trim();
        field.Label = model.Label.Trim();
        field.DataType = model.DataType;
        field.IsRequired = model.IsRequired;
        field.SortOrder = model.SortOrder;
        field.Options = model.DataType == FieldDataType.List ? string.Join(";", ParseOptions(model.Options)) : null;

        await _db.SaveChangesAsync();
        await AuditAsync("Update", nameof(FieldDefinition), field.Id, field.Name);

        TempData["Message"] = "Поле сохранено.";
        return RedirectToAction(nameof(Details), new { id = field.DocumentTypeId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleField(int id)
    {
        var field = await _db.FieldDefinitions.FindAsync(id);
        if (field == null) return NotFound();

        field.IsActive = !field.IsActive;
        await _db.SaveChangesAsync();
        await AuditAsync(field.IsActive ? "Activate" : "Deactivate", nameof(FieldDefinition), id, field.Name);

        return RedirectToAction(nameof(Details), new { id = field.DocumentTypeId });
    }

    // Поле без значений удаляется, поле со значениями только отключается (ФТ-5 из ТЗ)
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteField(int id)
    {
        var field = await _db.FieldDefinitions.FindAsync(id);
        if (field == null) return NotFound();

        var typeId = field.DocumentTypeId;
        var name = field.Name;

        if (await _db.FieldValues.AnyAsync(v => v.FieldDefinitionId == id))
        {
            field.IsActive = false;
            TempData["Message"] = "По полю уже есть значения, поэтому оно не удалено, а отключено.";
        }
        else
        {
            _db.FieldDefinitions.Remove(field);
            TempData["Message"] = "Поле удалено.";
        }

        await _db.SaveChangesAsync();
        await AuditAsync("DeleteField", nameof(FieldDefinition), id, name);

        return RedirectToAction(nameof(Details), new { id = typeId });
    }

    // ================= Вспомогательные методы =================

    private async Task<IActionResult> ShowDetails(int id, FieldFormModel? form)
    {
        var type = await _db.DocumentTypes
            .Include(t => t.Fields.OrderBy(f => f.SortOrder).ThenBy(f => f.Id))
            .FirstOrDefaultAsync(t => t.Id == id);
        if (type == null) return NotFound();

        form ??= new FieldFormModel
        {
            DocumentTypeId = id,
            SortOrder = type.Fields.Count == 0 ? 1 : type.Fields.Max(f => f.SortOrder) + 1
        };

        return View("Details", new TypeDetailsViewModel { Type = type, NewField = form });
    }

    private async Task ValidateFieldAsync(FieldFormModel model)
    {
        var name = (model.Name ?? "").Trim();

        if (await _db.FieldDefinitions.AnyAsync(f =>
                f.DocumentTypeId == model.DocumentTypeId && f.Name == name && f.Id != model.Id))
        {
            ModelState.AddModelError(nameof(model.Name), "В этом типе уже есть поле с таким именем");
        }

        if (model.DataType == FieldDataType.List && ParseOptions(model.Options).Count < 2)
        {
            ModelState.AddModelError(nameof(model.Options), "Для списка укажите минимум два варианта через «;»");
        }
    }

    private static List<string> ParseOptions(string? raw) =>
        (raw ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct()
            .ToList();

    // Запись в журнал действий (ФТ-15 из ТЗ)
    private async Task AuditAsync(string action, string entityType, int? entityId, string? details = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details
        });
        await _db.SaveChangesAsync();
    }
}