using System;
using System.Linq;
using HotChocolate;
using HotChocolate.Types;
using InventarioSilo.Data;
using InventarioSilo.GraphQL.Types;
using InventarioSilo.Models;
using MongoDB.Driver;

namespace InventarioSilo.GraphQL.Queries
{
    [ExtendObjectType(OperationTypeNames.Query)]
    public class ReporteQuery
    {
        public IEnumerable<ReporteMensual> ReporteMensual(
            DateTime desde,
            DateTime hasta,
            [Service] MongoDbContext context)
        {
            if (hasta < desde)
            {
                throw new GraphQLException("La fecha 'hasta' debe ser mayor o igual a 'desde'.");
            }

            var start = desde.Date;
            var end = hasta.Date.AddDays(1).AddTicks(-1);

            var itemsCollection = context.GetCollection<Item>("Items");
            var recepcionesCollection = context.GetCollection<Recepcion>("Recepciones");
            var entregasCollection = context.GetCollection<Entrega>("Entregas");

            var items = itemsCollection.Find(_ => true).ToList();

            var recepciones = recepcionesCollection
                .Find(r => r.Fecha >= start && r.Fecha <= end)
                .ToList();

            var entregas = entregasCollection
                .Find(e => e.Fecha >= start && e.Fecha <= end)
                .ToList();

            var entradasPorItemId = recepciones
                .Where(r => !string.IsNullOrWhiteSpace(r.ItemId))
                .GroupBy(r => r.ItemId!)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.CantidadRecibida));

            var entradasLegacyPorCodigo = recepciones
                .Where(r => string.IsNullOrWhiteSpace(r.ItemId))
                .GroupBy(r => r.CodigoMaterial)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.CantidadRecibida));

            var entradasSinRegistroPorItemId = recepciones
                .Where(r => r.EsSinRegistro && !string.IsNullOrWhiteSpace(r.ItemId))
                .GroupBy(r => r.ItemId!)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.CantidadRecibida));

            var entradasSinRegistroLegacyPorCodigo = recepciones
                .Where(r => r.EsSinRegistro && string.IsNullOrWhiteSpace(r.ItemId))
                .GroupBy(r => r.CodigoMaterial)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.CantidadRecibida));

            var salidasPorItemId = entregas
                .Where(e => !string.IsNullOrWhiteSpace(e.ItemId))
                .GroupBy(e => e.ItemId!)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.CantidadEntregada));

            var salidasLegacyPorCodigo = entregas
                .Where(e => string.IsNullOrWhiteSpace(e.ItemId))
                .GroupBy(e => e.CodigoMaterial)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.CantidadEntregada));

            var salidasSinRegistroPorItemId = entregas
                .Where(e => e.EsSinRegistro && !string.IsNullOrWhiteSpace(e.ItemId))
                .GroupBy(e => e.ItemId!)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.CantidadEntregada));

            var salidasSinRegistroLegacyPorCodigo = entregas
                .Where(e => e.EsSinRegistro && string.IsNullOrWhiteSpace(e.ItemId))
                .GroupBy(e => e.CodigoMaterial)
                .ToDictionary(g => g.Key, g => g.Sum(e => e.CantidadEntregada));

            foreach (var item in items)
            {
                var totalEntradas = 0m;
                var totalSalidas = 0m;
                var totalEntradasSinRegistro = 0m;
                var totalSalidasSinRegistro = 0m;

                if (!string.IsNullOrWhiteSpace(item.Id) && entradasPorItemId.TryGetValue(item.Id, out var entradasPorId))
                {
                    totalEntradas += entradasPorId;
                }

                if (entradasLegacyPorCodigo.TryGetValue(item.CodigoMaterial, out var entradasLegacy))
                {
                    totalEntradas += entradasLegacy;
                }

                if (!string.IsNullOrWhiteSpace(item.Id) && salidasPorItemId.TryGetValue(item.Id, out var salidasPorId))
                {
                    totalSalidas += salidasPorId;
                }

                if (salidasLegacyPorCodigo.TryGetValue(item.CodigoMaterial, out var salidasLegacy))
                {
                    totalSalidas += salidasLegacy;
                }

                if (!string.IsNullOrWhiteSpace(item.Id) && entradasSinRegistroPorItemId.TryGetValue(item.Id, out var entradasSrPorId))
                {
                    totalEntradasSinRegistro += entradasSrPorId;
                }

                if (entradasSinRegistroLegacyPorCodigo.TryGetValue(item.CodigoMaterial, out var entradasSrLegacy))
                {
                    totalEntradasSinRegistro += entradasSrLegacy;
                }

                if (!string.IsNullOrWhiteSpace(item.Id) && salidasSinRegistroPorItemId.TryGetValue(item.Id, out var salidasSrPorId))
                {
                    totalSalidasSinRegistro += salidasSrPorId;
                }

                if (salidasSinRegistroLegacyPorCodigo.TryGetValue(item.CodigoMaterial, out var salidasSrLegacy))
                {
                    totalSalidasSinRegistro += salidasSrLegacy;
                }

                yield return new ReporteMensual
                {
                    ItemId = item.Id,
                    CodigoMaterial = item.CodigoMaterial,
                    NombreMaterial = item.NombreMaterial,
                    DescripcionMaterial = item.DescripcionMaterial,
                    Localizacion = item.Localizacion ?? string.Empty,
                    TotalEntradas = totalEntradas,
                    TotalSalidas = totalSalidas,
                    UnidadMedida = item.UnidadMedida,
                    TotalEntradasSinRegistro = totalEntradasSinRegistro,
                    TotalSalidasSinRegistro = totalSalidasSinRegistro,
                    StockDespuesBalance = item.CantidadStock
                };
            }
        }
    }
}
