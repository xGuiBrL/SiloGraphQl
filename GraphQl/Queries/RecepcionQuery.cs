using HotChocolate.Types;
using InventarioSilo.Data;
using InventarioSilo.Models;
using MongoDB.Driver;

namespace InventarioSilo.GraphQL.Queries
{
    [ExtendObjectType(OperationTypeNames.Query)]
    public class RecepcionQuery
    {
        public IEnumerable<Recepcion> GetRecepciones(
            [Service] MongoDbContext context)
        {
            return context
                .GetCollection<Recepcion>("Recepciones")
                .Find(_ => true)
                .ToList();
        }

        public IEnumerable<Recepcion> GetRecepcionesPorCodigoMaterial(
            string codigoMaterial,
            [Service] MongoDbContext context)
        {
            return context
                .GetCollection<Recepcion>("Recepciones")
                .Find(r => r.CodigoMaterial == codigoMaterial)
                .ToList();
        }
    }
}
