using System.Security.Claims;
using Database2_Ilias_2.Models;
using Database2_Ilias_2.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

// Если у вас другое корневое пространство имён, замените Database2_Ilias_2.
namespace Database2_Ilias_2.Controllers;

// Любой вошедший пользователь может смотреть и искать документы (роль «читатель»).
// Создавать, менять и удалять могут только Admin и Editor.
[Authorize]
public class DocumentsController : Controller
{
    private const string EditorRoles = Roles.Admin + "," + Roles.Editor;

    private readonly DocumentService _service;

    public DocumentsController(DocumentService service) => _service = service;

    private string? CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private bool IsEditor => User.IsInRole(Roles.Admin) || User.IsInRole(Roles.Editor);

    // ----- Список и просмотр -----

    public async Task<IActionResult> Index(string? q, int? typeId, DocumentStatus? status, int page = 1)
        => View(await _service.ListAsync(q, typeId, status, page));

    public async Task<IActionResult> Details(int id)
    {
        var vm = await _service.GetDetailsAsync(id);
        if (vm == null) return NotFound();

        vm.CanEdit = IsEditor;
        vm.CanDelete = User.IsInRole(Roles.Admin) ||
                       (User.IsInRole(Roles.Editor) && vm.Document.CreatedByUserId == CurrentUserId);
        return View(vm);
    }

    // ----- Создание -----

    [Authorize(Roles = EditorRoles)]
    public async Task<IActionResult> ChooseType() => View(await _service.GetActiveTypesAsync());

    [Authorize(Roles = EditorRoles)]
    public async Task<IActionResult> Create(int typeId)
    {
        var model = await _service.BuildNewAsync(typeId);
        if (model == null) return NotFound();

        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = EditorRoles)]
    public async Task<IActionResult> Create(DocumentFormModel model)
    {
        var form = await _service.RebindAsync(model);
        if (form == null) return NotFound();

        foreach (var (key, message) in _service.Validate(form))
            ModelState.AddModelError(key, message);
        if (!ModelState.IsValid) return View("Form", form);

        var id = await _service.CreateAsync(form, CurrentUserId);
        TempData["Message"] = "Документ создан.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // ----- Редактирование -----

    [Authorize(Roles = EditorRoles)]
    public async Task<IActionResult> Edit(int id)
    {
        var model = await _service.BuildEditAsync(id);
        if (model == null) return NotFound();

        return View("Form", model);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = EditorRoles)]
    public async Task<IActionResult> Edit(DocumentFormModel model)
    {
        var form = await _service.RebindAsync(model);
        if (form == null) return NotFound();

        foreach (var (key, message) in _service.Validate(form))
            ModelState.AddModelError(key, message);
        if (!ModelState.IsValid) return View("Form", form);

        if (!await _service.UpdateAsync(form, CurrentUserId)) return NotFound();

        TempData["Message"] = "Изменения сохранены.";
        return RedirectToAction(nameof(Details), new { id = form.Id });
    }

    // ----- Удаление (в корзину) -----

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = EditorRoles)]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.SoftDeleteAsync(id, CurrentUserId, User.IsInRole(Roles.Admin));

        switch (result)
        {
            case DeleteResult.NotFound:  return NotFound();
            case DeleteResult.Forbidden: return Forbid();
        }

        TempData["Message"] = "Документ перемещён в корзину.";
        return RedirectToAction(nameof(Index));
    }
}