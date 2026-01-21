using InventarioSilo.Data;
using InventarioSilo.Models;
using InventarioSilo.GraphQL.Inputs;
using InventarioSilo.GraphQL.Validation;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using MongoDB.Driver;

namespace InventarioSilo.GraphQL.Mutations
{
    [ExtendObjectType(OperationTypeNames.Mutation)]
    [Authorize]
    public class ItemMutation
    {
        private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(-4);
        private const string ManualAdjustmentLabel = "S/R";

        public async Task<Item> CrearItem(
            ItemInput input,
            [Service] MongoDbContext context)
        {
            var sanitized = InputValidator.NormalizeItemInput(input);

            var items = context.GetCollection<Item>("Items");
            await EnsureUniqueMaterialCodeAsync(items, sanitized.CodigoMaterial);

            var item = new Item
            {
                CodigoMaterial = sanitized.CodigoMaterial,
                NombreMaterial = sanitized.NombreMaterial,
                DescripcionMaterial = sanitized.DescripcionMaterial,
                CantidadStock = sanitized.CantidadStock,
                Localizacion = sanitized.Localizacion,
                UnidadMedida = sanitized.UnidadMedida
            };

            await items.InsertOneAsync(item);

            return item;
        }

        public async Task<Item> ActualizarItem(
            ItemUpdateInput input,
            [Service] MongoDbContext context)
        {
            var sanitized = InputValidator.NormalizeItemInput(input);
            var id = InputValidator.NormalizeId(input.Id);
            var items = context.GetCollection<Item>("Items");

            await EnsureUniqueMaterialCodeAsync(items, sanitized.CodigoMaterial, id);

            var existingItem = await items
                .Find(i => i.Id == id)
                .FirstOrDefaultAsync();

            if (existingItem is null)
            {
                throw new GraphQLException("Item no encontrado");
            }

            var stockDelta = sanitized.CantidadStock - existingItem.CantidadStock;

            var update = Builders<Item>.Update
                .Set(i => i.CodigoMaterial, sanitized.CodigoMaterial)
                .Set(i => i.NombreMaterial, sanitized.NombreMaterial)
                .Set(i => i.DescripcionMaterial, sanitized.DescripcionMaterial)
                .Set(i => i.CantidadStock, sanitized.CantidadStock)
                .Set(i => i.Localizacion, sanitized.Localizacion)
                .Set(i => i.UnidadMedida, sanitized.UnidadMedida);

            var updatedItem = await items.FindOneAndUpdateAsync(
                i => i.Id == id,
                update,
                new FindOneAndUpdateOptions<Item>
                {
                    ReturnDocument = ReturnDocument.After
                });

            if (updatedItem is null)
            {
                throw new GraphQLException("Item no encontrado");
            }

            if (stockDelta != 0)
            {
                await RegistrarMovimientoSinRegistroAsync(context, updatedItem, stockDelta);
            }

            return updatedItem;
        }

        public async Task<bool> EliminarItem(
            string id,
            [Service] MongoDbContext context)
        {
            var normalizedId = InputValidator.NormalizeId(id);
            var items = context.GetCollection<Item>("Items");
            var result = await items.DeleteOneAsync(i => i.Id == normalizedId);

            if (result.DeletedCount == 0)
            {
                throw new GraphQLException("Item no encontrado");
            }

            return true;
        }

        private static async Task EnsureUniqueMaterialCodeAsync(
            IMongoCollection<Item> items,
            string codigoMaterial,
            string? excludeId = null)
        {
            var filter = Builders<Item>.Filter.Eq(i => i.CodigoMaterial, codigoMaterial);

            if (!string.IsNullOrEmpty(excludeId))
            {
                filter &= Builders<Item>.Filter.Ne(i => i.Id, excludeId);
            }

            var duplicate = await items.Find(filter).FirstOrDefaultAsync();
            if (duplicate is not null)
            {
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("Ya existe un item con el mismo código de material.")
                    .SetCode("VALIDATION_ERROR")
                    .SetExtension("field", "codigoMaterial")
                    .Build());
            }
        }

        private static async Task RegistrarMovimientoSinRegistroAsync(
            MongoDbContext context,
            Item item,
            decimal stockDelta)
        {
            if (stockDelta == 0)
            {
                return;
            }

            var itemId = EnsureItemId(item);
            var timestamp = DateTimeOffset.UtcNow
                .ToOffset(LocalOffset)
                .DateTime;

            if (stockDelta > 0)
            {
                var recepciones = context.GetCollection<Recepcion>("Recepciones");
                var recepcion = new Recepcion
                {
                    ItemId = itemId,
                    Fecha = timestamp,
                    RecibidoDe = ManualAdjustmentLabel,
                    CodigoMaterial = item.CodigoMaterial,
                    DescripcionMaterial = ManualAdjustmentLabel,
                    CantidadRecibida = Math.Abs(stockDelta),
                    UnidadMedida = item.UnidadMedida,
                    Observaciones = ManualAdjustmentLabel,
                    EsSinRegistro = true
                };

                await recepciones.InsertOneAsync(recepcion);
            }
            else
            {
                var entregas = context.GetCollection<Entrega>("Entregas");
                var entrega = new Entrega
                {
                    ItemId = itemId,
                    Fecha = timestamp,
                    EntregadoA = ManualAdjustmentLabel,
                    CodigoMaterial = item.CodigoMaterial,
                    DescripcionMaterial = ManualAdjustmentLabel,
                    CantidadEntregada = Math.Abs(stockDelta),
                    UnidadMedida = item.UnidadMedida,
                    Observaciones = ManualAdjustmentLabel,
                    EsSinRegistro = true
                };

                await entregas.InsertOneAsync(entrega);
            }
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
