// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using System;
using Microsoft.Extensions.Options;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    public class DirectOptionsSnapshot<TOptions> : IOptionsSnapshot<TOptions> where TOptions : class, new()
    {
        public DirectOptionsSnapshot(TOptions actualValue)
        {
            Value = actualValue;
        }

        public TOptions Value { get; }

        public TOptions Get(string name)
        {
            throw new NotImplementedException("Shouldn't be needed for integration tests");
        }
    }
}
