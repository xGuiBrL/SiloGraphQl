using System.Globalization;
using HotChocolate;
using HotChocolate.Authorization;
using InventarioSilo.Data;
using InventarioSilo.GraphQL.Inputs;
using InventarioSilo.Models;
using InventarioSilo.Services;
using MongoDB.Driver;

namespace InventarioSilo.GraphQL.Mutations
{
    [ExtendObjectType(OperationTypeNames.Mutation)]
    public class AuthMutation
    {
        [Authorize]
        public async Task<Usuario> RegistrarUsuario(
            RegistrarUsuarioInput input,
            [Service] MongoDbContext context)
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
                Password = input.Password,
                Nombre = nombreFormateado
            };

            await context.Usuarios.InsertOneAsync(usuario);

            return usuario;
        }

        [AllowAnonymous]
        public async Task<string> Login(
            string usuario,
            string password,
            [Service] MongoDbContext context,
            [Service] JwtService jwtService)
        {
            if (string.IsNullOrWhiteSpace(usuario))
                throw new GraphQLException("El usuario es obligatorio.");
            if (string.IsNullOrWhiteSpace(password))
                throw new GraphQLException("La contraseña es obligatoria.");

            var normalizado = usuario.Trim().ToLowerInvariant();
            var usuarioDb = await context.Usuarios.Find(u => u.NombreUsuario == normalizado).FirstOrDefaultAsync();
            if (usuarioDb == null || usuarioDb.Password != password)
                throw new GraphQLException("Usuario o contraseña incorrectos.");

            return jwtService.GenerateToken(usuarioDb);
        }
    }
}
