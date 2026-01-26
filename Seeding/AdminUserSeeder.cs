using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InventarioSilo.Data;
using InventarioSilo.Models;
using InventarioSilo.Security;
using InventarioSilo.Services;
using InventarioSilo.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace InventarioSilo.Seeding
{
    public static class AdminUserSeeder
    {
        public static async Task EnsureAdminUserAsync(IServiceProvider services)
        {
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<MongoDbContext>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();
            var logger = scope.ServiceProvider.GetService<ILoggerFactory>()?.CreateLogger(nameof(AdminUserSeeder));
            var settings = scope.ServiceProvider.GetService<IOptions<AdminSeedSettings>>()?.Value;

            if (!HasValidSettings(settings))
            {
                logger?.LogInformation("Admin seed configuration missing. Skipping admin seeding.");
                return;
            }

            var usuarios = context.Usuarios;
            var adminUsername = settings!.Usuario.Trim().ToLowerInvariant();
            var existente = await usuarios.Find(u => u.NombreUsuario == adminUsername).FirstOrDefaultAsync();

            if (existente is not null)
            {
                var updates = new List<UpdateDefinition<Usuario>>();

                if (!string.Equals(existente.Rol, UserRoles.Admin, StringComparison.OrdinalIgnoreCase))
                {
                    updates.Add(Builders<Usuario>.Update.Set(u => u.Rol, UserRoles.Admin));
                }

                if (!string.Equals(existente.Nombre, settings.Nombre, StringComparison.Ordinal))
                {
                    updates.Add(Builders<Usuario>.Update.Set(u => u.Nombre, settings.Nombre));
                }

                if (!passwordHasher.LooksHashed(existente.Password))
                {
                    var hashed = passwordHasher.HashPassword(settings.Password);
                    updates.Add(Builders<Usuario>.Update.Set(u => u.Password, hashed));
                }

                if (updates.Count > 0)
                {
                    var update = Builders<Usuario>.Update.Combine(updates);
                    await usuarios.UpdateOneAsync(u => u.Id == existente.Id, update);
                    logger?.LogInformation("Usuario admin existente actualizado.");
                }

                return;
            }

            var admin = new Usuario
            {
                Nombre = settings.Nombre,
                NombreUsuario = adminUsername,
                Password = passwordHasher.HashPassword(settings.Password),
                Rol = UserRoles.Admin
            };

            await usuarios.InsertOneAsync(admin);
            logger?.LogInformation("Usuario admin creado mediante seeding.");
        }

        private static bool HasValidSettings(AdminSeedSettings? seed)
        {
            return seed is not null
                && !string.IsNullOrWhiteSpace(seed.Usuario)
                && !string.IsNullOrWhiteSpace(seed.Password)
                && !string.IsNullOrWhiteSpace(seed.Nombre);
        }
    }
}
