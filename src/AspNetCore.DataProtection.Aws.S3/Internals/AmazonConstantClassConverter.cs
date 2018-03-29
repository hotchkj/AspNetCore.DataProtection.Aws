// Copyright(c) 2018 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Amazon.Runtime;

namespace AspNetCore.DataProtection.Aws.S3.Internals
{
    // ReSharper disable once InheritdocConsiderUsage
    /// <summary>
    /// Allows natural typing of S3 configuration values continuing the pattern AWS SDK uses as though the constants are enums, but are in fact classes.
    /// </summary>
    /// <remarks>
    /// Takes advantage of the fact that the various 'enum' values are public static fields on the relevant constants.
    /// </remarks>
    /// <typeparam name="TConstantClass"><see cref="ConstantClass"/> derivation to convert between.</typeparam>
    public class AmazonConstantClassConverter<TConstantClass> : TypeConverter where TConstantClass : ConstantClass
    {
        private static readonly IReadOnlyDictionary<string, TConstantClass> ClassLookup;
        private static readonly IReadOnlyDictionary<TConstantClass, string> StringLookup;

        static AmazonConstantClassConverter()
        {
            FieldInfo[] fields = typeof(TConstantClass).GetFields(BindingFlags.Public | BindingFlags.Static);

            ClassLookup = fields.Where(x => x.FieldType == typeof(TConstantClass)).ToDictionary(x => x.Name, x => (TConstantClass)x.GetValue(null));
            StringLookup = ClassLookup.ToDictionary(x => x.Value, x => x.Key);
        }

        /// <inheritdoc />
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        /// <inheritdoc />
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        /// <inheritdoc />
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string s)
            {
                return ClassLookup[s];
            }
            return base.ConvertFrom(context, culture, value);
        }

        /// <inheritdoc />
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is TConstantClass)
            {
                return StringLookup[(TConstantClass)value];
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
