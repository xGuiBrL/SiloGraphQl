using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using HotChocolate;
using InventarioSilo.GraphQL.Inputs;
using InventarioSilo.Models;
using MongoDB.Bson;

namespace InventarioSilo.GraphQL.Validation
{
    public static class InputValidator
    {
        public const decimal ItemMinStock = 0m;
        public const decimal ItemMaxStock = 999_999m;
        public const decimal MovementMin = 0.01m;
        public const decimal MovementMax = 999_999m;

        private static readonly Regex CodeRegex = new("^[A-Z0-9-]+$", RegexOptions.Compiled);
        private static readonly Regex PlainTextRegex = new(@"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ0-9.,()/\'\-\s]+$", RegexOptions.Compiled);
        private static readonly Dictionary<string, string> AllowedUnits = new(StringComparer.OrdinalIgnoreCase)
        {
            ["LT"] = "Lt",
            ["KG"] = "Kg",
            ["MTS"] = "Mts",
            ["UND"] = "Und",
            ["QQ"] = "QQ",
            ["PQTES"] = "Pqtes",
            ["PZAS"] = "Pzas",
            ["MT2"] = "Mt2",
            ["MT3"] = "Mt3"
        };

        public record ItemPayload(
            string CategoriaId,
            string UbicacionId,
            string CodigoMaterial,
            string DescripcionMaterial,
            decimal CantidadStock,
            string UnidadMedida);

        public record CategoriaPayload(
            string Id,
            string Nombre,
            string Descripcion);

        public record UbicacionPayload(
            string Id,
            string Nombre,
            string Descripcion);

        public record MovementPayload(
            string Id,
            string CodigoMaterial,
            string? ItemId,
            string Responsable,
            string DescripcionMaterial,
            string UnidadMedida,
            decimal Cantidad,
            string? Observaciones);

        public static ItemPayload NormalizeItemInput(ItemInput input)
        {
            if (input is null)
            {
                throw BuildValidationError("item", "Los datos del item son obligatorios.");
            }

            return new ItemPayload(
                NormalizeObjectId(input.CategoriaId, "categoriaId"),
                NormalizeObjectId(input.UbicacionId, "ubicacionId"),
                NormalizeCode(input.CodigoMaterial, "codigoMaterial", 25, allowSpaces: true),
                NormalizePlainText(input.DescripcionMaterial, "descripcionMaterial", 140, titleCase: false, allowAnyChar: true),
                NormalizeQuantity(input.CantidadStock, "cantidadStock", ItemMinStock, ItemMaxStock),
                NormalizeUnit(input.UnidadMedida, "unidadMedida"));
        }

        public static CategoriaPayload NormalizeCategoriaInput(CategoriaInput input)
        {
            if (input is null)
            {
                throw BuildValidationError("categoria", "Los datos de la categoría son obligatorios.");
            }

            var nombre = NormalizePlainText(input.Nombre, "nombre", 60, titleCase: true);
            var descripcion = string.IsNullOrWhiteSpace(input.Descripcion)
                ? string.Empty
                : NormalizePlainText(input.Descripcion, "descripcion", 140, titleCase: false);

            return new CategoriaPayload(
                string.Empty,
                nombre,
                descripcion);
        }

        public static CategoriaPayload NormalizeCategoriaUpdateInput(CategoriaUpdateInput input)
        {
            var basePayload = NormalizeCategoriaInput(input);
            return basePayload with { Id = NormalizeObjectId(input.Id, "id") };
        }

        public static UbicacionPayload NormalizeUbicacionInput(UbicacionInput input)
        {
            if (input is null)
            {
                throw BuildValidationError("ubicacion", "Los datos de la ubicación son obligatorios.");
            }

            var nombre = NormalizePlainText(input.Nombre, "nombre", 60, titleCase: true);
            var descripcion = string.IsNullOrWhiteSpace(input.Descripcion)
                ? string.Empty
                : NormalizePlainText(input.Descripcion, "descripcion", 140, titleCase: false);

            return new UbicacionPayload(
                string.Empty,
                nombre,
                descripcion);
        }

        public static UbicacionPayload NormalizeUbicacionUpdateInput(UbicacionUpdateInput input)
        {
            var basePayload = NormalizeUbicacionInput(input);
            return basePayload with { Id = NormalizeObjectId(input.Id, "id") };
        }

        public static MovementPayload NormalizeRecepcionInput(RecepcionInput input)
        {
            if (input is null)
            {
                throw BuildValidationError("recepcion", "Los datos de la recepción son obligatorios.");
            }

            return new MovementPayload(
                string.Empty,
                NormalizeCode(input.CodigoMaterial, "codigoMaterial", 25, allowSpaces: true),
                NormalizeOptionalObjectId(input.ItemId, "itemId"),
                NormalizePlainText(input.RecibidoDe, "recibidoDe", 60, titleCase: true, preserveTrailingSpace: true),
                NormalizePlainText(input.DescripcionMaterial, "descripcionMaterial", 140, titleCase: false, allowAnyChar: true),
                NormalizeUnit(input.UnidadMedida, "unidadMedida"),
                NormalizeQuantity(input.CantidadRecibida, "cantidadRecibida", MovementMin, MovementMax),
                NormalizeOptionalText(input.Observaciones, 220, allowAnyChar: true));
        }

        public static MovementPayload NormalizeRecepcionUpdateInput(RecepcionUpdateInput input)
        {
            var basePayload = NormalizeRecepcionInput(input);
            var id = NormalizeId(input.Id);
            return basePayload with { Id = id };
        }

        public static MovementPayload NormalizeEntregaInput(EntregaInput input)
        {
            if (input is null)
            {
                throw BuildValidationError("entrega", "Los datos de la entrega son obligatorios.");
            }

            return new MovementPayload(
                string.Empty,
                NormalizeCode(input.CodigoMaterial, "codigoMaterial", 25, allowSpaces: true),
                NormalizeOptionalObjectId(input.ItemId, "itemId"),
                NormalizePlainText(input.EntregadoA, "entregadoA", 60, titleCase: true),
                NormalizePlainText(input.DescripcionMaterial, "descripcionMaterial", 140, titleCase: false, allowAnyChar: true),
                NormalizeUnit(input.UnidadMedida, "unidadMedida"),
                NormalizeQuantity(input.CantidadEntregada, "cantidadEntregada", MovementMin, MovementMax),
                NormalizeOptionalText(input.Observaciones, 220, allowAnyChar: true));
        }

        public static MovementPayload NormalizeEntregaUpdateInput(EntregaUpdateInput input)
        {
            var basePayload = NormalizeEntregaInput(input);
            var id = NormalizeId(input.Id);
            return basePayload with { Id = id };
        }

        public static string NormalizeId(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw BuildValidationError("id", "El identificador es obligatorio.");
            }

            return id.Trim();
        }

        public static string NormalizeObjectId(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw BuildValidationError(field, "El identificador es obligatorio.");
            }

            if (!ObjectId.TryParse(value, out var bsonId))
            {
                throw BuildValidationError(field, "Identificador inválido.");
            }

            return bsonId.ToString();
        }

        public static string? NormalizeOptionalObjectId(string? value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (!ObjectId.TryParse(value, out var bsonId))
            {
                throw BuildValidationError(field, "Identificador inválido.");
            }

            return bsonId.ToString();
        }

        public static void EnsureItemSnapshotMatches(Item item, MovementPayload payload, string context)
        {
            if (!string.Equals(item.CodigoMaterial, payload.CodigoMaterial, StringComparison.OrdinalIgnoreCase))
            {
                throw BuildValidationError("codigoMaterial", $"El código de material no coincide con el item seleccionado para {context}.");
            }

            if (!string.Equals(item.DescripcionMaterial, payload.DescripcionMaterial, StringComparison.OrdinalIgnoreCase))
            {
                throw BuildValidationError("descripcionMaterial", $"La descripción no coincide con el item seleccionado para {context}.");
            }

            if (!string.Equals(item.UnidadMedida, payload.UnidadMedida, StringComparison.OrdinalIgnoreCase))
            {
                throw BuildValidationError("unidadMedida", $"La unidad de medida no coincide con el item seleccionado para {context}.");
            }
        }

        private static string NormalizeCode(string? value, string field, int maxLength, bool allowSpaces = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw BuildValidationError(field, "Este campo es obligatorio.");
            }

            var pattern = allowSpaces ? "[^A-Z0-9-\\s]" : "[^A-Z0-9-]";
            var normalized = Regex.Replace(value.ToUpperInvariant(), pattern, string.Empty);
            if (allowSpaces)
            {
                normalized = Regex.Replace(normalized, "\\s+", " ").Trim();
            }

            if (normalized.Length == 0)
            {
                var message = allowSpaces
                    ? "Usa letras, números, guiones o espacios."
                    : "Usa únicamente letras, números o guiones.";
                throw BuildValidationError(field, message);
            }

            if (normalized.Length > maxLength)
            {
                normalized = normalized[..maxLength];
            }

            return normalized;
        }

        private static string NormalizeUnit(string? value, string field)
        {
            var normalized = NormalizeCode(value, field, 10);
            if (!AllowedUnits.TryGetValue(normalized, out var canonical))
            {
                throw BuildValidationError(field, "Selecciona una unidad válida (Lt, Kg, Mts, Und, QQ, Pqtes, Pzas, Mt2 o Mt3).");
            }

            return canonical;
        }

        private static string NormalizePlainText(string? value, string field, int maxLength, bool titleCase, bool preserveTrailingSpace = false, bool allowAnyChar = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw BuildValidationError(field, "Este campo es obligatorio.");
            }

            var hadTrailingSpace = preserveTrailingSpace && Regex.IsMatch(value, "\\s$", RegexOptions.Singleline);
            var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
            if (!allowAnyChar && !PlainTextRegex.IsMatch(normalized))
            {
                throw BuildValidationError(field, "Se encontraron caracteres no permitidos.");
            }

            if (normalized.Length > maxLength)
            {
                normalized = normalized[..maxLength];
            }

            if (titleCase)
            {
                normalized = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(normalized.ToLowerInvariant());
            }

            if (preserveTrailingSpace && hadTrailingSpace && normalized.Length < maxLength)
            {
                normalized += " ";
            }

            return normalized;
        }

        private static string? NormalizeOptionalText(string? value, int maxLength, bool allowAnyChar = false)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
            if (!allowAnyChar)
            {
                normalized = Regex.Replace(normalized, @"[^A-Za-zÁÉÍÓÚÜÑáéíóúüñ0-9.,()/\'\-\s]", string.Empty);
            }

            if (normalized.Length > maxLength)
            {
                normalized = normalized[..maxLength];
            }

            return normalized;
        }

        private static decimal NormalizeQuantity(decimal value, string field, decimal min, decimal max)
        {
            if (value < min || value > max)
            {
                throw BuildValidationError(field, $"Ingresa un número entre {min} y {max}.");
            }

            return Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        private static GraphQLException BuildValidationError(string field, string message)
        {
            return new GraphQLException(ErrorBuilder.New()
                .SetMessage(message)
                .SetCode("VALIDATION_ERROR")
                .SetExtension("field", field)
                .Build());
        }
    }
}
