using System.Globalization;
using System.Text.RegularExpressions;
using HotChocolate;
using InventarioSilo.GraphQL.Inputs;
using InventarioSilo.Models;

namespace InventarioSilo.GraphQL.Validation
{
    public static class InputValidator
    {
        public const decimal ItemMinStock = 0m;
        public const decimal ItemMaxStock = 999_999m;
        public const decimal MovementMin = 0.01m;
        public const decimal MovementMax = 999_999m;

        private static readonly Regex CodeRegex = new("^[A-Z0-9-]+$", RegexOptions.Compiled);
        private static readonly Regex PlainTextRegex = new(@"^[A-Za-zÁÉÍÓÚÜÑáéíóúüñ0-9.,()'\-\s]+$", RegexOptions.Compiled);

        public record ItemPayload(
            string CodigoMaterial,
            string NombreMaterial,
            string DescripcionMaterial,
            decimal CantidadStock,
            string Localizacion,
            string UnidadMedida);

        public record MovementPayload(
            string Id,
            string CodigoMaterial,
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
                NormalizeCode(input.CodigoMaterial, "codigoMaterial", 25, allowSpaces: true),
                NormalizePlainText(input.NombreMaterial, "nombreMaterial", 60, titleCase: true),
                NormalizePlainText(input.DescripcionMaterial, "descripcionMaterial", 140, titleCase: false),
                NormalizeQuantity(input.CantidadStock, "cantidadStock", ItemMinStock, ItemMaxStock),
                NormalizePlainText(input.Localizacion, "localizacion", 40, titleCase: true),
                NormalizeCode(input.UnidadMedida, "unidadMedida", 10));
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
                NormalizePlainText(input.RecibidoDe, "recibidoDe", 60, titleCase: true),
                NormalizePlainText(input.DescripcionMaterial, "descripcionMaterial", 140, titleCase: false),
                NormalizeCode(input.UnidadMedida, "unidadMedida", 10),
                NormalizeQuantity(input.CantidadRecibida, "cantidadRecibida", MovementMin, MovementMax),
                NormalizeOptionalText(input.Observaciones, 220));
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
                NormalizePlainText(input.EntregadoA, "entregadoA", 60, titleCase: true),
                NormalizePlainText(input.DescripcionMaterial, "descripcionMaterial", 140, titleCase: false),
                NormalizeCode(input.UnidadMedida, "unidadMedida", 10),
                NormalizeQuantity(input.CantidadEntregada, "cantidadEntregada", MovementMin, MovementMax),
                NormalizeOptionalText(input.Observaciones, 220));
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

        private static string NormalizePlainText(string? value, string field, int maxLength, bool titleCase)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw BuildValidationError(field, "Este campo es obligatorio.");
            }

            var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
            if (!PlainTextRegex.IsMatch(normalized))
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

            return normalized;
        }

        private static string? NormalizeOptionalText(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var normalized = Regex.Replace(value.Trim(), @"\s+", " ");
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
