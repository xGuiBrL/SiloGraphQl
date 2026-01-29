using InventarioSilo.Data;
using InventarioSilo.Models;
using InventarioSilo.GraphQL.Inputs;
using InventarioSilo.GraphQL.Validation;
using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using MongoDB.Bson;
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
            var categorias = context.Categorias;
            var ubicaciones = context.Ubicaciones;

            var categoria = await GetCategoriaOrThrowAsync(categorias, sanitized.CategoriaId);
            var ubicacion = await GetUbicacionOrThrowAsync(ubicaciones, sanitized.UbicacionId);

            var item = new Item
            {
                CategoriaId = categoria.Id!,
                UbicacionId = ubicacion.Id!,
                CodigoMaterial = sanitized.CodigoMaterial,
                NombreMaterial = categoria.Nombre,
                DescripcionMaterial = sanitized.DescripcionMaterial,
                CantidadStock = sanitized.CantidadStock,
                Localizacion = ubicacion.Nombre,
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
            var categorias = context.Categorias;
            var ubicaciones = context.Ubicaciones;

            var existingItem = await items
                .Find(i => i.Id == id)
                .FirstOrDefaultAsync();

            if (existingItem is null)
            {
                throw new GraphQLException("Item no encontrado");
            }

            var stockDelta = sanitized.CantidadStock - existingItem.CantidadStock;
            var categoria = await GetCategoriaOrThrowAsync(categorias, sanitized.CategoriaId);
            var ubicacion = await GetUbicacionOrThrowAsync(ubicaciones, sanitized.UbicacionId);

            var snapshotWillChange =
                !string.Equals(existingItem.CodigoMaterial, sanitized.CodigoMaterial, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existingItem.DescripcionMaterial, sanitized.DescripcionMaterial, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(existingItem.UnidadMedida, sanitized.UnidadMedida, StringComparison.OrdinalIgnoreCase);

            var update = Builders<Item>.Update
                .Set(i => i.CategoriaId, categoria.Id!)
                .Set(i => i.UbicacionId, ubicacion.Id!)
                .Set(i => i.CodigoMaterial, sanitized.CodigoMaterial)
                .Set(i => i.NombreMaterial, categoria.Nombre)
                .Set(i => i.DescripcionMaterial, sanitized.DescripcionMaterial)
                .Set(i => i.CantidadStock, sanitized.CantidadStock)
                .Set(i => i.Localizacion, ubicacion.Nombre)
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

            if (snapshotWillChange)
            {
                await SynchronizeMovementSnapshotsAsync(context, existingItem, updatedItem);
            }

            return updatedItem;
        }

        public async Task<bool> EliminarItem(
            string id,
            [Service] MongoDbContext context)
        {
            var normalizedId = InputValidator.NormalizeId(id);
            var items = context.GetCollection<Item>("Items");
            var existingItem = await items
                .Find(i => i.Id == normalizedId)
                .FirstOrDefaultAsync();

            if (existingItem is null)
            {
                throw new GraphQLException("Item no encontrado");
            }

            var codigoMaterial = existingItem.CodigoMaterial?.Trim() ?? string.Empty;
            var recepciones = context.GetCollection<Recepcion>("Recepciones");
            var entregas = context.GetCollection<Entrega>("Entregas");

            var recepcionesTask = recepciones.DeleteManyAsync(
                BuildRecepcionCascadeFilter(normalizedId, codigoMaterial));
            var entregasTask = entregas.DeleteManyAsync(
                BuildEntregaCascadeFilter(normalizedId, codigoMaterial));

            await Task.WhenAll(recepcionesTask, entregasTask);

            var result = await items.DeleteOneAsync(i => i.Id == normalizedId);

            if (result.DeletedCount == 0)
            {
                throw new GraphQLException("Item no encontrado");
            }

            return true;
        }

        private static FilterDefinition<Recepcion> BuildRecepcionCascadeFilter(string itemId, string codigoMaterial)
        {
            var builder = Builders<Recepcion>.Filter;
            var byItem = builder.Eq(r => r.ItemId, itemId);
            var legacyWithoutItem = BuildLegacyCascadeFilter(builder, codigoMaterial);

            return builder.Or(byItem, legacyWithoutItem);
        }

        private static FilterDefinition<Entrega> BuildEntregaCascadeFilter(string itemId, string codigoMaterial)
        {
            var builder = Builders<Entrega>.Filter;
            var byItem = builder.Eq(e => e.ItemId, itemId);
            var legacyWithoutItem = BuildLegacyCascadeFilter(builder, codigoMaterial);

            return builder.Or(byItem, legacyWithoutItem);
        }

        private static FilterDefinition<T> BuildLegacyCascadeFilter<T>(
            FilterDefinitionBuilder<T> builder,
            string codigoMaterial)
        {
            var legacyDoc = new BsonDocument
            {
                {
                    "$and",
                    new BsonArray
                    {
                        new BsonDocument("CodigoMaterial", codigoMaterial),
                        new BsonDocument
                        {
                            {
                                "$or",
                                new BsonArray
                                {
                                    new BsonDocument("ItemId", BsonNull.Value),
                                    new BsonDocument("ItemId", string.Empty),
                                    new BsonDocument("ItemId", new BsonDocument("$exists", false))
                                }
                            }
                        }
                    }
                }
            };

            return new BsonDocumentFilterDefinition<T>(legacyDoc);
        }

        private static async Task SynchronizeMovementSnapshotsAsync(
            MongoDbContext context,
            Item previousItem,
            Item updatedItem)
        {
            var itemId = EnsureItemId(updatedItem);
            var previousCode = previousItem.CodigoMaterial?.Trim() ?? string.Empty;

            var recepciones = context.GetCollection<Recepcion>("Recepciones");
            var entregas = context.GetCollection<Entrega>("Entregas");

            var recepcionFilter = BuildRecepcionCascadeFilter(itemId, previousCode);
            var entregaFilter = BuildEntregaCascadeFilter(itemId, previousCode);

            var recepcionUpdate = Builders<Recepcion>.Update
                .Set(r => r.ItemId, itemId)
                .Set(r => r.CodigoMaterial, updatedItem.CodigoMaterial)
                .Set(r => r.DescripcionMaterial, updatedItem.DescripcionMaterial)
                .Set(r => r.UnidadMedida, updatedItem.UnidadMedida);

            var entregaUpdate = Builders<Entrega>.Update
                .Set(e => e.ItemId, itemId)
                .Set(e => e.CodigoMaterial, updatedItem.CodigoMaterial)
                .Set(e => e.DescripcionMaterial, updatedItem.DescripcionMaterial)
                .Set(e => e.UnidadMedida, updatedItem.UnidadMedida);

            await Task.WhenAll(
                recepciones.UpdateManyAsync(recepcionFilter, recepcionUpdate),
                entregas.UpdateManyAsync(entregaFilter, entregaUpdate));
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

        private static async Task<Categoria> GetCategoriaOrThrowAsync(
            IMongoCollection<Categoria> categorias,
            string categoriaId)
        {
            var categoria = await categorias
                .Find(c => c.Id == categoriaId)
                .FirstOrDefaultAsync();

            if (categoria is null)
            {
                throw BuildCategoriaNotFoundError();
            }

            return categoria;
        }

        private static GraphQLException BuildCategoriaNotFoundError()
        {
            return new GraphQLException(ErrorBuilder.New()
                .SetMessage("La categoría seleccionada no existe.")
                .SetCode("VALIDATION_ERROR")
                .SetExtension("field", "categoriaId")
                .Build());
        }

        private static async Task<Ubicacion> GetUbicacionOrThrowAsync(
            IMongoCollection<Ubicacion> ubicaciones,
            string ubicacionId)
        {
            var ubicacion = await ubicaciones
                .Find(u => u.Id == ubicacionId)
                .FirstOrDefaultAsync();

            if (ubicacion is null)
            {
                throw BuildUbicacionNotFoundError();
            }

            return ubicacion;
        }

        private static GraphQLException BuildUbicacionNotFoundError()
        {
            return new GraphQLException(ErrorBuilder.New()
                .SetMessage("La ubicación seleccionada no existe.")
                .SetCode("VALIDATION_ERROR")
                .SetExtension("field", "ubicacionId")
                .Build());
        }
    }
}
