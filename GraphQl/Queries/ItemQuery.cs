using HotChocolate.Types;
using InventarioSilo.Data;
using InventarioSilo.Models;
using MongoDB.Driver;

namespace InventarioSilo.GraphQL.Queries
{
    [ExtendObjectType(OperationTypeNames.Query)]
    public class ItemQuery
    {
        public IEnumerable<Item> GetItems(
            [Service] MongoDbContext context,
            bool soloConStock = false)
        {
            var items = context.GetCollection<Item>("Items");
            var filter = soloConStock
                ? Builders<Item>.Filter.Gt(i => i.CantidadStock, 0)
                : Builders<Item>.Filter.Empty;

            return items
                .Find(filter)
                .SortBy(i => i.NombreMaterial)
                .ThenBy(i => i.Localizacion)
                .ThenBy(i => i.DescripcionMaterial)
                .ToList();
        }

        public Item? GetItemPorCodigoMaterial(
            string codigoMaterial,
            [Service] MongoDbContext context)
        {
            return context
                .GetCollection<Item>("Items")
                .Find(i => i.CodigoMaterial == codigoMaterial)
                .FirstOrDefault();
        }
    }
}
