using InventarioSilo.Data;
using InventarioSilo.GraphQL.Inputs;
using InventarioSilo.GraphQL.Validation;
using InventarioSilo.Models;
using MongoDB.Driver;
using HotChocolate;
using HotChocolate.Types;

namespace InventarioSilo.GraphQL.Mutations
{
    [ExtendObjectType(OperationTypeNames.Mutation)]
    public class EntregaMutation
    {
        public async Task<Entrega> CrearEntrega(
            EntregaInput input,
            [Service] MongoDbContext context)
        {
            var sanitized = InputValidator.NormalizeEntregaInput(input);
            var items = context.GetCollection<Item>("Items");
            var entregas = context.GetCollection<Entrega>("Entregas");

            var item = await GetItemByCodigoMaterialAsync(items, sanitized.CodigoMaterial);
            var itemId = EnsureItemId(item);

            InputValidator.EnsureItemSnapshotMatches(item, sanitized, "la entrega");

            if (item.CantidadStock < sanitized.Cantidad)
                throw new GraphQLException("Stock insuficiente");

            await items.UpdateOneAsync(
                i => i.Id == itemId,
                Builders<Item>.Update.Inc(i => i.CantidadStock, -sanitized.Cantidad));

            var entrega = new Entrega
            {
                ItemId = itemId,
                Fecha = DateTimeOffset.UtcNow
                    .ToOffset(TimeSpan.FromHours(-4))
                    .DateTime,
                EntregadoA = sanitized.Responsable,
                CodigoMaterial = item.CodigoMaterial,
                DescripcionMaterial = item.DescripcionMaterial,
                CantidadEntregada = sanitized.Cantidad,
                UnidadMedida = item.UnidadMedida,
                Observaciones = sanitized.Observaciones
            };

            await entregas.InsertOneAsync(entrega);

            return entrega;
        }

        public async Task<Entrega> ActualizarEntrega(
            EntregaUpdateInput input,
            [Service] MongoDbContext context)
        {
            var sanitized = InputValidator.NormalizeEntregaUpdateInput(input);
            var items = context.GetCollection<Item>("Items");
            var entregas = context.GetCollection<Entrega>("Entregas");

            var existing = await entregas
                .Find(e => e.Id == sanitized.Id)
                .FirstOrDefaultAsync();

            if (existing is null)
                throw new GraphQLException("Entrega no encontrada");

            var originalItem = await GetItemByIdOrCodigoAsync(items, existing.ItemId, existing.CodigoMaterial);
            var originalItemId = EnsureItemId(originalItem);

            Item targetItem;
            string targetItemId;

            var movingToDifferentItem = !string.Equals(originalItem.CodigoMaterial, sanitized.CodigoMaterial, StringComparison.OrdinalIgnoreCase);

            if (movingToDifferentItem)
            {
                await items.UpdateOneAsync(
                    i => i.Id == originalItemId,
                    Builders<Item>.Update.Inc(i => i.CantidadStock, existing.CantidadEntregada));

                var newItem = await GetItemByCodigoMaterialAsync(items, sanitized.CodigoMaterial);
                var newItemId = EnsureItemId(newItem);

                InputValidator.EnsureItemSnapshotMatches(newItem, sanitized, "la entrega");

                if (newItem.CantidadStock < sanitized.Cantidad)
                    throw new GraphQLException("Stock insuficiente");

                await items.UpdateOneAsync(
                    i => i.Id == newItemId,
                    Builders<Item>.Update.Inc(i => i.CantidadStock, -sanitized.Cantidad));

                targetItem = newItem;
                targetItemId = newItemId;
            }
            else
            {
                InputValidator.EnsureItemSnapshotMatches(originalItem, sanitized, "la entrega");

                var delta = sanitized.Cantidad - existing.CantidadEntregada;
                if (delta != 0)
                {
                    if (delta > 0 && originalItem.CantidadStock < delta)
                        throw new GraphQLException("Stock insuficiente");

                    await items.UpdateOneAsync(
                        i => i.Id == originalItemId,
                        Builders<Item>.Update.Inc(i => i.CantidadStock, -delta));
                }

                targetItem = originalItem;
                targetItemId = originalItemId;
            }

            var update = Builders<Entrega>.Update
                .Set(e => e.ItemId, targetItemId)
                .Set(e => e.EntregadoA, sanitized.Responsable)
                .Set(e => e.CodigoMaterial, targetItem.CodigoMaterial)
                .Set(e => e.DescripcionMaterial, targetItem.DescripcionMaterial)
                .Set(e => e.CantidadEntregada, sanitized.Cantidad)
                .Set(e => e.UnidadMedida, targetItem.UnidadMedida)
                .Set(e => e.Observaciones, sanitized.Observaciones);

            var updatedEntrega = await entregas.FindOneAndUpdateAsync(
                e => e.Id == sanitized.Id,
                update,
                new FindOneAndUpdateOptions<Entrega>
                {
                    ReturnDocument = ReturnDocument.After
                });

            return updatedEntrega ?? throw new GraphQLException("Entrega no encontrada");
        }

        public async Task<bool> EliminarEntrega(
            string id,
            [Service] MongoDbContext context)
        {
            var normalizedId = InputValidator.NormalizeId(id);
            var items = context.GetCollection<Item>("Items");
            var entregas = context.GetCollection<Entrega>("Entregas");

            var existing = await entregas
                .Find(e => e.Id == normalizedId)
                .FirstOrDefaultAsync();

            if (existing is null)
                throw new GraphQLException("Entrega no encontrada");

            var item = await GetItemByIdOrCodigoAsync(items, existing.ItemId, existing.CodigoMaterial);
            var itemId = EnsureItemId(item);

            await items.UpdateOneAsync(
                i => i.Id == itemId,
                Builders<Item>.Update.Inc(i => i.CantidadStock, existing.CantidadEntregada));

            await entregas.DeleteOneAsync(e => e.Id == normalizedId);

            return true;
        }

        private static async Task<Item> GetItemByCodigoMaterialAsync(IMongoCollection<Item> items, string codigoMaterial)
        {
            var item = await items
                .Find(i => i.CodigoMaterial == codigoMaterial)
                .FirstOrDefaultAsync();

            if (item is null)
            {
                throw new GraphQLException("Item no encontrado");
            }

            return item;
        }

        private static async Task<Item> GetItemByIdOrCodigoAsync(IMongoCollection<Item> items, string? itemId, string codigoMaterial)
        {
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                var byId = await items.Find(i => i.Id == itemId).FirstOrDefaultAsync();
                if (byId is not null)
                {
                    return byId;
                }
            }

            return await GetItemByCodigoMaterialAsync(items, codigoMaterial);
        }

        private static string EnsureItemId(Item item)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
            {
                throw new GraphQLException("El item no tiene un identificador válido.");
            }

            return item.Id;
        }
    }
}
