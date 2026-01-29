using HotChocolate;
using HotChocolate.Types;
using InventarioSilo.Data;
using InventarioSilo.GraphQL.Types;
using InventarioSilo.Models;
using MongoDB.Driver;

namespace InventarioSilo.GraphQL.Queries
{
    [ExtendObjectType(OperationTypeNames.Query)]
    public class KardexQuery
    {
        public async Task<KardexItem> KardexPorCodigoMaterial(
            string codigoMaterial,
            string? itemId,
            [Service] MongoDbContext context)
        {
            var items = context.GetCollection<Item>("Items");
            var entregas = context.GetCollection<Entrega>("Entregas");
            var recepciones = context.GetCollection<Recepcion>("Recepciones");

            Item? item = null;

            if (!string.IsNullOrWhiteSpace(itemId))
            {
                item = await items
                    .Find(i => i.Id == itemId)
                    .FirstOrDefaultAsync();
            }

            item ??= await items
                .Find(i => i.CodigoMaterial == codigoMaterial)
                .FirstOrDefaultAsync();

            if (item == null)
                throw new GraphQLException("Item no encontrado");

            var resolvedItemId = EnsureItemId(item);

            var movimientos = new List<KardexMovimiento>();

            var entradas = recepciones
                .Find(r => r.ItemId == resolvedItemId || (string.IsNullOrEmpty(r.ItemId) && r.CodigoMaterial == codigoMaterial))
                .ToList();

            movimientos.AddRange(entradas.Select(r => new KardexMovimiento
            {
                Fecha = r.Fecha,
                Tipo = "ENTRADA",
                Referencia = r.RecibidoDe,
                Descripcion = r.DescripcionMaterial,
                Observaciones = r.Observaciones,
                Cantidad = r.CantidadRecibida,
                UnidadMedida = r.UnidadMedida,
                Origen = "RECEPCION",
                RegistroId = r.Id,
                EsSinRegistro = r.EsSinRegistro
            }));

            var salidas = entregas
                .Find(e => e.ItemId == resolvedItemId || (string.IsNullOrEmpty(e.ItemId) && e.CodigoMaterial == codigoMaterial))
                .ToList();

            movimientos.AddRange(salidas.Select(e => new KardexMovimiento
            {
                Fecha = e.Fecha,
                Tipo = "SALIDA",
                Referencia = e.EntregadoA,
                Descripcion = e.DescripcionMaterial,
                Observaciones = e.Observaciones,
                Cantidad = e.CantidadEntregada,
                UnidadMedida = e.UnidadMedida,
                Origen = "ENTREGA",
                RegistroId = e.Id,
                EsSinRegistro = e.EsSinRegistro
            }));

            movimientos = movimientos
                .OrderBy(m => m.Fecha)
                .ToList();

            return new KardexItem
            {
                CodigoMaterial = item.CodigoMaterial,
                NombreMaterial = item.NombreMaterial,
                StockActual = item.CantidadStock,
                Movimientos = movimientos
            };
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
