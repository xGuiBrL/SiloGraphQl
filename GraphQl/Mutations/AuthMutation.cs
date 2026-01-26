using System.Collections.Generic;
using System.Globalization;
using HotChocolate;
using HotChocolate.Authorization;
using InventarioSilo.Data;
using InventarioSilo.GraphQL.Inputs;
using InventarioSilo.Models;
using InventarioSilo.Security;
using InventarioSilo.Services;
using MongoDB.Driver;
using InventarioSilo.GraphQL.Validation;

namespace InventarioSilo.GraphQL.Mutations
{
    [ExtendObjectType(OperationTypeNames.Mutation)]
    public class AuthMutation
    {
        [Authorize(Roles = new[] { UserRoles.Admin })]
        public async Task<Usuario> RegistrarUsuario(
            RegistrarUsuarioInput input,
            [Service] MongoDbContext context,
            [Service] PasswordHasher passwordHasher)
        {
            if (input == null)
                throw new GraphQLException("La información del usuario es obligatoria.");

            if (string.IsNullOrWhiteSpace(input.Usuario))
                throw new GraphQLException("El usuario es obligatorio.");

            if (string.IsNullOrWhiteSpace(input.Password) || input.Password.Length < 6)
                throw new GraphQLException("La contraseña debe tener al menos 6 caracteres.");

            if (string.IsNullOrWhiteSpace(input.Nombre))
                throw new GraphQLException("El nombre es obligatorio.");

            var normalizado = input.Usuario.Trim().ToLowerInvariant();
            var existente = await context.Usuarios.Find(u => u.NombreUsuario == normalizado).FirstOrDefaultAsync();
            if (existente != null)
                throw new GraphQLException("Ya existe un usuario con ese nombre.");

            var nombreFormateado = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.Nombre.Trim().ToLowerInvariant());

            var usuario = new Usuario
            {
                NombreUsuario = normalizado,
                Password = passwordHasher.HashPassword(input.Password),
                Nombre = nombreFormateado,
                Rol = UserRoles.Usuario
            };

            await context.Usuarios.InsertOneAsync(usuario);

            return usuario;
        }

        [AllowAnonymous]
        public async Task<string> Login(
            string usuario,
            string password,
            [Service] MongoDbContext context,
            [Service] JwtService jwtService,
            [Service] PasswordHasher passwordHasher)
        {
            if (string.IsNullOrWhiteSpace(usuario))
                throw new GraphQLException("El usuario es obligatorio.");
            if (string.IsNullOrWhiteSpace(password))
                throw new GraphQLException("La contraseña es obligatoria.");

            var normalizado = usuario.Trim().ToLowerInvariant();
            var usuarioDb = await context.Usuarios.Find(u => u.NombreUsuario == normalizado).FirstOrDefaultAsync();
            if (usuarioDb == null)
                throw new GraphQLException("Usuario o contraseña incorrectos.");

            var storedPassword = usuarioDb.Password ?? string.Empty;
            var passwordValida = passwordHasher.VerifyPassword(password, storedPassword);
            if (!passwordValida)
                throw new GraphQLException("Usuario o contraseña incorrectos.");

            var updates = new List<UpdateDefinition<Usuario>>();

            if (!passwordHasher.LooksHashed(storedPassword))
            {
                var hashed = passwordHasher.HashPassword(password);
                usuarioDb.Password = hashed;
                updates.Add(Builders<Usuario>.Update.Set(u => u.Password, hashed));
            }

            if (string.IsNullOrWhiteSpace(usuarioDb.Rol))
            {
                usuarioDb.Rol = UserRoles.Usuario;
                updates.Add(Builders<Usuario>.Update.Set(u => u.Rol, usuarioDb.Rol));
            }

            if (updates.Count > 0 && !string.IsNullOrWhiteSpace(usuarioDb.Id))
            {
                var combined = Builders<Usuario>.Update.Combine(updates);
                await context.Usuarios.UpdateOneAsync(u => u.Id == usuarioDb.Id, combined);
            }

            return jwtService.GenerateToken(usuarioDb);
        }

        [Authorize(Roles = new[] { UserRoles.Admin })]
        public async Task<Usuario> EditarUsuario(
            ActualizarUsuarioInput input,
            [Service] MongoDbContext context,
            [Service] PasswordHasher passwordHasher)
        {
            if (input is null)
            {
                throw new GraphQLException("Los datos del usuario son obligatorios.");
            }

            var normalizedId = InputValidator.NormalizeObjectId(input.Id, "id");
            var normalizedUsuario = NormalizeUsuario(input.Usuario);
            var normalizedNombre = NormalizeNombre(input.Nombre);
            var normalizedRol = NormalizeRol(input.Rol);
            var normalizedPassword = NormalizePasswordIfProvided(input.Password);

            var usuarios = context.Usuarios;
            var usuarioDb = await usuarios.Find(u => u.Id == normalizedId).FirstOrDefaultAsync();
            if (usuarioDb is null)
            {
                throw new GraphQLException("Usuario no encontrado.");
            }

            if (!string.Equals(usuarioDb.NombreUsuario, normalizedUsuario, StringComparison.OrdinalIgnoreCase))
            {
                var duplicate = await usuarios
                    .Find(u => u.NombreUsuario == normalizedUsuario && u.Id != normalizedId)
                    .FirstOrDefaultAsync();

                if (duplicate is not null)
                {
                    throw new GraphQLException("Ya existe un usuario con ese nombre.");
                }
            }

            var updates = new List<UpdateDefinition<Usuario>>
            {
                Builders<Usuario>.Update.Set(u => u.NombreUsuario, normalizedUsuario),
                Builders<Usuario>.Update.Set(u => u.Nombre, normalizedNombre),
                Builders<Usuario>.Update.Set(u => u.Rol, normalizedRol)
            };

            if (normalizedPassword is not null)
            {
                var hashed = passwordHasher.HashPassword(normalizedPassword);
                updates.Add(Builders<Usuario>.Update.Set(u => u.Password, hashed));
            }

            var updateDefinition = Builders<Usuario>.Update.Combine(updates);
            var updated = await usuarios.FindOneAndUpdateAsync(
                u => u.Id == normalizedId,
                updateDefinition,
                new FindOneAndUpdateOptions<Usuario>
                {
                    ReturnDocument = ReturnDocument.After
                });

            return updated ?? throw new GraphQLException("No fue posible actualizar el usuario.");
        }

        [Authorize(Roles = new[] { UserRoles.Admin })]
        public async Task<bool> EliminarUsuario(
            string id,
            [Service] MongoDbContext context)
        {
            var normalizedId = InputValidator.NormalizeObjectId(id, "id");
            var result = await context.Usuarios.DeleteOneAsync(u => u.Id == normalizedId);

            if (result.DeletedCount == 0)
            {
                throw new GraphQLException("Usuario no encontrado.");
            }

            return true;
        }

        private static string NormalizeUsuario(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new GraphQLException("El usuario es obligatorio.");
            }

            return value.Trim().ToLowerInvariant();
        }

        private static string NormalizeNombre(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new GraphQLException("El nombre es obligatorio.");
            }

            var text = value.Trim();
            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text.ToLowerInvariant());
        }

        private static string NormalizeRol(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new GraphQLException("El rol es obligatorio.");
            }

            var normalized = value.Trim().ToLowerInvariant();
            if (normalized != UserRoles.Admin && normalized != UserRoles.Usuario)
            {
                throw new GraphQLException("Rol inválido. Usa admin o usuario.");
            }

            return normalized;
        }

        private static string? NormalizePasswordIfProvided(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (value.Length < 6)
            {
                throw new GraphQLException("La contraseña debe tener al menos 6 caracteres.");
            }

            return value;
        }
    }
}
