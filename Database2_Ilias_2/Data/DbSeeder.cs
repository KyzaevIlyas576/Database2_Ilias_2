using Database2_Ilias_2;
using Database2_Ilias_2.Data;
using Database2_Ilias_2.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

// Оставьте namespace таким же, как в ApplicationDbContext.cs (ИмяПроекта.Data).
namespace Database2_Ilias_2.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        await db.Database.MigrateAsync();   // применяет миграции, если они ещё не применены

        // --- Роли ---
        foreach (var role in new[] { Roles.Admin, Roles.Editor, Roles.Reader })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        // --- Администратор ---
        // Логин и пароль можно переопределить в appsettings.json (раздел SeedAdmin)
        // или в user-secrets. Пароль по умолчанию — только для учебного запуска.
        var email = config["SeedAdmin:Email"] ?? "admin@example.com";
        var password = config["SeedAdmin:Password"] ?? "Admin123!";

        var admin = await userManager.FindByEmailAsync(email);
        if (admin == null)
        {
            admin = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            var result = await userManager.CreateAsync(admin, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Не удалось создать администратора: " +
                    string.Join("; ", result.Errors.Select(e => e.Description)));
            await userManager.AddToRoleAsync(admin, Roles.Admin);
        }

        // --- Примеры типов документов ---
        if (!await db.DocumentTypes.AnyAsync())
        {
            db.DocumentTypes.AddRange(
                new DocumentType
                {
                    Name = "Письмо",
                    Description = "Входящая или исходящая корреспонденция",
                    Fields =
                    {
                        new FieldDefinition { Name = "recipient",       Label = "Адресат",          DataType = FieldDataType.Text,    IsRequired = true, SortOrder = 1 },
                        new FieldDefinition { Name = "outgoing_number", Label = "Исходящий номер",  DataType = FieldDataType.Text,    SortOrder = 2 },
                        new FieldDefinition { Name = "sent_date",       Label = "Дата отправки",    DataType = FieldDataType.Date,    IsRequired = true, SortOrder = 3 },
                        new FieldDefinition { Name = "reply_required",  Label = "Требуется ответ",  DataType = FieldDataType.Boolean, SortOrder = 4 },
                    }
                },
                new DocumentType
                {
                    Name = "Протокол",
                    Description = "Протокол заседания или совещания",
                    Fields =
                    {
                        new FieldDefinition { Name = "meeting_date", Label = "Дата заседания", DataType = FieldDataType.Date, IsRequired = true, SortOrder = 1 },
                        new FieldDefinition { Name = "chairman",     Label = "Председатель",   DataType = FieldDataType.Text, IsRequired = true, SortOrder = 2 },
                        new FieldDefinition { Name = "secretary",    Label = "Секретарь",      DataType = FieldDataType.Text, SortOrder = 3 },
                        new FieldDefinition { Name = "participants", Label = "Участники",      DataType = FieldDataType.Text, SortOrder = 4 },
                    }
                },
                new DocumentType
                {
                    Name = "Договор",
                    Description = "Договор между сторонами",
                    Fields =
                    {
                        new FieldDefinition { Name = "number",         Label = "Номер договора",  DataType = FieldDataType.Text,   IsRequired = true, SortOrder = 1 },
                        new FieldDefinition { Name = "parties",        Label = "Стороны",         DataType = FieldDataType.Text,   IsRequired = true, SortOrder = 2 },
                        new FieldDefinition { Name = "amount",         Label = "Сумма",           DataType = FieldDataType.Number, SortOrder = 3 },
                        new FieldDefinition { Name = "valid_until",    Label = "Действует до",    DataType = FieldDataType.Date,   SortOrder = 4 },
                        new FieldDefinition { Name = "signing_status", Label = "Статус подписания", DataType = FieldDataType.List,
                                              Options = "Не подписан;Подписан одной стороной;Подписан", SortOrder = 5 },
                    }
                });

            await db.SaveChangesAsync();
        }
    }
}