using HotChocolate.Types;
using InventarioSilo.Data;
using InventarioSilo.GraphQL.Validation;
using InventarioSilo.Models;
using MongoDB.Driver;

namespace InventarioSilo.GraphQL.Queries
{
    [ExtendObjectType(OperationTypeNames.Query)]
    public class CategoriaQuery
    {
        public IEnumerable<Categoria> GetCategorias(
            [Service] MongoDbContext context)
        {
            return context.Categorias
                .Find(_ => true)
                .SortBy(c => c.Nombre)
                .ToList();
        }

        public Categoria? GetCategoriaPorId(
            string id,
            [Service] MongoDbContext context)
        {
            var normalizedId = InputValidator.NormalizeObjectId(id, "id");
            return context.Categorias
                .Find(c => c.Id == normalizedId)
                .FirstOrDefault();
        }
    }
}
