using System.Security.Claims;
using HotChocolate;
using HotChocolate.Authorization;
using InventarioSilo.Data;
using InventarioSilo.Models;
using MongoDB.Driver;

namespace InventarioSilo.GraphQL.Queries
{
    [ExtendObjectType(OperationTypeNames.Query)]
    public class AuthQuery
    {
        [Authorize]
        public async Task<Usuario> PerfilActual(
            ClaimsPrincipal principal,
            [Service] MongoDbContext context)
        {
            var userId = principal.FindFirst("id")?.Value;
            if (string.IsNullOrEmpty(userId))
                throw new GraphQLException("Token inválido.");

            var usuario = await context.Usuarios
                .Find(u => u.Id == userId)
                .FirstOrDefaultAsync();

            if (usuario == null)
                throw new GraphQLException("Usuario no encontrado.");

            return usuario;
        }
    }
}
