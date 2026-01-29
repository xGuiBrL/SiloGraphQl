using InventarioSilo.Data;
using InventarioSilo.Models;
using InventarioSilo.GraphQL.Inputs;
using InventarioSilo.GraphQL.Validation;
using MongoDB.Driver;
using HotChocolate;
using HotChocolate.Types;

namespace InventarioSilo.GraphQL.Mutations
{
    [ExtendObjectType(OperationTypeNames.Mutation)]
    public class RecepcionMutation
    {
        public async Task<Recepcion> CrearRecepcion(
            RecepcionInput input,
            [Service] MongoDbContext context)
        {
            var sanitized = InputValidator.NormalizeRecepcionInput(input);
            var items = context.GetCollection<Item>("Items");
            var recepciones = context.GetCollection<Recepcion>("Recepciones");

            var item = await GetItemByIdOrCodigoAsync(items, sanitized.ItemId, sanitized.CodigoMaterial);
            var itemId = EnsureItemId(item);

            InputValidator.EnsureItemSnapshotMatches(item, sanitized, "la recepción");

            // ➕ Sumar stock
            await items.UpdateOneAsync(
                i => i.Id == itemId,
                Builders<Item>.Update.Inc(i => i.CantidadStock, sanitized.Cantidad));

            var recepcion = new Recepcion
            {
                ItemId = itemId,
                Fecha = DateTimeOffset.UtcNow
                    .ToOffset(TimeSpan.FromHours(-4))
                    .DateTime,
                RecibidoDe = sanitized.Responsable,
                CodigoMaterial = item.CodigoMaterial,
                DescripcionMaterial = item.DescripcionMaterial,
                CantidadRecibida = sanitized.Cantidad,
                UnidadMedida = item.UnidadMedida,
                Observaciones = sanitized.Observaciones
            };

            await recepciones.InsertOneAsync(recepcion);

            return recepcion;
        }

        public async Task<Recepcion> ActualizarRecepcion(
            RecepcionUpdateInput input,
            [Service] MongoDbContext context)
        {
            var sanitized = InputValidator.NormalizeRecepcionUpdateInput(input);
            var items = context.GetCollection<Item>("Items");
            var recepciones = context.GetCollection<Recepcion>("Recepciones");

            var existing = await recepciones
                .Find(r => r.Id == sanitized.Id)
                .FirstOrDefaultAsync();

            if (existing is null)
                throw new GraphQLException("Recepción no encontrada");

            var originalItem = await GetItemByIdOrCodigoAsync(items, existing.ItemId, existing.CodigoMaterial);
            var originalItemId = EnsureItemId(originalItem);

            Item targetItem;
            string targetItemId;

            var movingToDifferentItem = !string.IsNullOrEmpty(sanitized.ItemId)
                ? !string.Equals(originalItemId, sanitized.ItemId, StringComparison.Ordinal)
                : !string.Equals(originalItem.CodigoMaterial, sanitized.CodigoMaterial, StringComparison.OrdinalIgnoreCase);

            if (movingToDifferentItem)
            {
                if (originalItem.CantidadStock < existing.CantidadRecibida)
                    throw new GraphQLException("El stock actual no permite editar este registro");

                await items.UpdateOneAsync(
                    i => i.Id == originalItemId,
                    Builders<Item>.Update.Inc(i => i.CantidadStock, -existing.CantidadRecibida));

                var newItem = await GetItemByIdOrCodigoAsync(items, sanitized.ItemId, sanitized.CodigoMaterial);
                var newItemId = EnsureItemId(newItem);

                InputValidator.EnsureItemSnapshotMatches(newItem, sanitized, "la recepción");

                await items.UpdateOneAsync(
                    i => i.Id == newItemId,
                    Builders<Item>.Update.Inc(i => i.CantidadStock, sanitized.Cantidad));

                targetItem = newItem;
                targetItemId = newItemId;
            }
            else
            {
                InputValidator.EnsureItemSnapshotMatches(originalItem, sanitized, "la recepción");

                var delta = sanitized.Cantidad - existing.CantidadRecibida;
                if (delta != 0)
                {
                    if (originalItem.CantidadStock + delta < 0)
                        throw new GraphQLException("El stock actual no permite editar este registro");

                    await items.UpdateOneAsync(
                        i => i.Id == originalItemId,
                        Builders<Item>.Update.Inc(i => i.CantidadStock, delta));
                }

                targetItem = originalItem;
                targetItemId = originalItemId;
            }

            var update = Builders<Recepcion>.Update
                .Set(r => r.ItemId, targetItemId)
                .Set(r => r.RecibidoDe, sanitized.Responsable)
                .Set(r => r.CodigoMaterial, targetItem.CodigoMaterial)
                .Set(r => r.DescripcionMaterial, targetItem.DescripcionMaterial)
                .Set(r => r.CantidadRecibida, sanitized.Cantidad)
                .Set(r => r.UnidadMedida, targetItem.UnidadMedida)
                .Set(r => r.Observaciones, sanitized.Observaciones);

            var updatedRecepcion = await recepciones.FindOneAndUpdateAsync(
                r => r.Id == sanitized.Id,
                update,
                new FindOneAndUpdateOptions<Recepcion>
                {
                    ReturnDocument = ReturnDocument.After
                });

            return updatedRecepcion ?? throw new GraphQLException("Recepción no encontrada");
        }

        public async Task<bool> EliminarRecepcion(
            string id,
            [Service] MongoDbContext context)
        {
            var normalizedId = InputValidator.NormalizeId(id);
            var items = context.GetCollection<Item>("Items");
            var recepciones = context.GetCollection<Recepcion>("Recepciones");

            var existing = await recepciones
                .Find(r => r.Id == normalizedId)
                .FirstOrDefaultAsync();

            if (existing is null)
                throw new GraphQLException("Recepción no encontrada");

            var item = await GetItemByIdOrCodigoAsync(items, existing.ItemId, existing.CodigoMaterial);
            var itemId = EnsureItemId(item);

            if (item.CantidadStock < existing.CantidadRecibida)
                throw new GraphQLException("No hay stock suficiente para eliminar este registro");

            await items.UpdateOneAsync(
                i => i.Id == itemId,
                Builders<Item>.Update.Inc(i => i.CantidadStock, -existing.CantidadRecibida));

            await recepciones.DeleteOneAsync(r => r.Id == normalizedId);

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
