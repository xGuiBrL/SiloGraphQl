using HotChocolate.Types;
using InventarioSilo.Data;
using InventarioSilo.GraphQL.Validation;
using InventarioSilo.Models;
using MongoDB.Driver;

namespace InventarioSilo.GraphQL.Queries
{
    [ExtendObjectType(OperationTypeNames.Query)]
    public class UbicacionQuery
    {
        public IEnumerable<Ubicacion> GetUbicaciones(
            [Service] MongoDbContext context)
        {
            return context.Ubicaciones
                .Find(_ => true)
                .SortBy(u => u.Nombre)
                .ToList();
        }

        public Ubicacion? GetUbicacionPorId(
            string id,
            [Service] MongoDbContext context)
        {
            var normalizedId = InputValidator.NormalizeObjectId(id, "id");
            return context.Ubicaciones
                .Find(u => u.Id == normalizedId)
                .FirstOrDefault();
        }
    }
}
