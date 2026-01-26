using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Types;
using InventarioSilo.Data;
using InventarioSilo.GraphQL.Inputs;
using InventarioSilo.GraphQL.Validation;
using InventarioSilo.Models;
using MongoDB.Driver;

namespace InventarioSilo.GraphQL.Mutations
{
    [ExtendObjectType(OperationTypeNames.Mutation)]
    [Authorize]
    public class UbicacionMutation
    {
        public async Task<Ubicacion> CrearUbicacion(
            UbicacionInput input,
            [Service] MongoDbContext context)
        {
            var sanitized = InputValidator.NormalizeUbicacionInput(input);
            var ubicaciones = context.Ubicaciones;

            await EnsureUniqueNombreAsync(ubicaciones, sanitized.Nombre);

            var ubicacion = new Ubicacion
            {
                Nombre = sanitized.Nombre,
                Descripcion = sanitized.Descripcion
            };

            await ubicaciones.InsertOneAsync(ubicacion);
            return ubicacion;
        }

        public async Task<Ubicacion> ActualizarUbicacion(
            UbicacionUpdateInput input,
            [Service] MongoDbContext context)
        {
            var sanitized = InputValidator.NormalizeUbicacionUpdateInput(input);
            var ubicaciones = context.Ubicaciones;

            await EnsureUniqueNombreAsync(ubicaciones, sanitized.Nombre, sanitized.Id);

            var update = Builders<Ubicacion>.Update
                .Set(u => u.Nombre, sanitized.Nombre)
                .Set(u => u.Descripcion, sanitized.Descripcion);

            var updated = await ubicaciones.FindOneAndUpdateAsync(
                u => u.Id == sanitized.Id,
                update,
                new FindOneAndUpdateOptions<Ubicacion>
                {
                    ReturnDocument = ReturnDocument.After
                });

            if (updated is null)
            {
                throw new GraphQLException("Ubicación no encontrada");
            }

            var items = context.GetCollection<Item>("Items");
            await items.UpdateManyAsync(
                i => i.UbicacionId == sanitized.Id,
                Builders<Item>.Update.Set(i => i.Localizacion, sanitized.Nombre));

            return updated;
        }

        public async Task<bool> EliminarUbicacion(
            string id,
            [Service] MongoDbContext context)
        {
            var normalizedId = InputValidator.NormalizeObjectId(id, "id");
            var ubicaciones = context.Ubicaciones;
            var items = context.GetCollection<Item>("Items");

            var inUse = await items
                .Find(i => i.UbicacionId == normalizedId)
                .Limit(1)
                .AnyAsync();

            if (inUse)
            {
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("No puedes eliminar la ubicación mientras existan items asociados.")
                    .SetCode("VALIDATION_ERROR")
                    .SetExtension("field", "ubicacionId")
                    .Build());
            }

            var result = await ubicaciones.DeleteOneAsync(u => u.Id == normalizedId);
            if (result.DeletedCount == 0)
            {
                throw new GraphQLException("Ubicación no encontrada");
            }

            return true;
        }

        private static async Task EnsureUniqueNombreAsync(
            IMongoCollection<Ubicacion> ubicaciones,
            string nombre,
            string? excludeId = null)
        {
            var filter = Builders<Ubicacion>.Filter.Eq(u => u.Nombre, nombre);

            if (!string.IsNullOrEmpty(excludeId))
            {
                filter &= Builders<Ubicacion>.Filter.Ne(u => u.Id, excludeId);
            }

            var existing = await ubicaciones.Find(filter).FirstOrDefaultAsync();
            if (existing is not null)
            {
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("Ya existe una ubicación con el mismo nombre.")
                    .SetCode("VALIDATION_ERROR")
                    .SetExtension("field", "nombre")
                    .Build());
            }
        }
    }
}
