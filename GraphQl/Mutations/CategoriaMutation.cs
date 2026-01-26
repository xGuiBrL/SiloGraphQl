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
    public class CategoriaMutation
    {
        public async Task<Categoria> CrearCategoria(
            CategoriaInput input,
            [Service] MongoDbContext context)
        {
            var sanitized = InputValidator.NormalizeCategoriaInput(input);
            var categorias = context.Categorias;

            await EnsureUniqueNombreAsync(categorias, sanitized.Nombre);

            var categoria = new Categoria
            {
                Nombre = sanitized.Nombre,
                Descripcion = sanitized.Descripcion
            };

            await categorias.InsertOneAsync(categoria);
            return categoria;
        }

        public async Task<Categoria> ActualizarCategoria(
            CategoriaUpdateInput input,
            [Service] MongoDbContext context)
        {
            var sanitized = InputValidator.NormalizeCategoriaUpdateInput(input);
            var categorias = context.Categorias;

            await EnsureUniqueNombreAsync(categorias, sanitized.Nombre, sanitized.Id);

            var update = Builders<Categoria>.Update
                .Set(c => c.Nombre, sanitized.Nombre)
                .Set(c => c.Descripcion, sanitized.Descripcion);

            var updated = await categorias.FindOneAndUpdateAsync(
                c => c.Id == sanitized.Id,
                update,
                new FindOneAndUpdateOptions<Categoria>
                {
                    ReturnDocument = ReturnDocument.After
                });

            if (updated is null)
            {
                throw new GraphQLException("Categoría no encontrada");
            }

            var items = context.GetCollection<Item>("Items");
            await items.UpdateManyAsync(
                i => i.CategoriaId == sanitized.Id,
                Builders<Item>.Update.Set(i => i.NombreMaterial, sanitized.Nombre));

            return updated;
        }

        public async Task<bool> EliminarCategoria(
            string id,
            [Service] MongoDbContext context)
        {
            var normalizedId = InputValidator.NormalizeObjectId(id, "id");
            var categorias = context.Categorias;
            var items = context.GetCollection<Item>("Items");

            var inUse = await items
                .Find(i => i.CategoriaId == normalizedId)
                .Limit(1)
                .AnyAsync();

            if (inUse)
            {
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("No puedes eliminar la categoría mientras existan items asociados.")
                    .SetCode("VALIDATION_ERROR")
                    .SetExtension("field", "categoriaId")
                    .Build());
            }

            var result = await categorias.DeleteOneAsync(c => c.Id == normalizedId);
            if (result.DeletedCount == 0)
            {
                throw new GraphQLException("Categoría no encontrada");
            }

            return true;
        }

        private static async Task EnsureUniqueNombreAsync(
            IMongoCollection<Categoria> categorias,
            string nombre,
            string? excludeId = null)
        {
            var filter = Builders<Categoria>.Filter.Eq(c => c.Nombre, nombre);

            if (!string.IsNullOrEmpty(excludeId))
            {
                filter &= Builders<Categoria>.Filter.Ne(c => c.Id, excludeId);
            }

            var existing = await categorias.Find(filter).FirstOrDefaultAsync();
            if (existing is not null)
            {
                throw new GraphQLException(ErrorBuilder.New()
                    .SetMessage("Ya existe una categoría con el mismo nombre.")
                    .SetCode("VALIDATION_ERROR")
                    .SetExtension("field", "nombre")
                    .Build());
            }
        }
    }
}
