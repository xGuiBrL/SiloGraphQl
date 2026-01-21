using HotChocolate.Types;
using InventarioSilo.Data;
using InventarioSilo.Models;
using MongoDB.Driver;

namespace InventarioSilo.GraphQL.Queries
{
    [ExtendObjectType(OperationTypeNames.Query)]
    public class EntregaQuery
    {
        public IEnumerable<Entrega> GetEntregas(
            [Service] MongoDbContext context)
        {
            return context
                .GetCollection<Entrega>("Entregas")
                .Find(_ => true)
                .ToList();
        }

        public IEnumerable<Entrega> GetEntregasPorCodigoMaterial(
            string codigoMaterial,
            [Service] MongoDbContext context)
        {
            return context
                .GetCollection<Entrega>("Entregas")
                .Find(e => e.CodigoMaterial == codigoMaterial)
                .ToList();
        }
    }
}
