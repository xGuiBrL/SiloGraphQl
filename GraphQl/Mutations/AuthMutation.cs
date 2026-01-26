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
    }
}
