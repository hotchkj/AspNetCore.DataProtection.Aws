// Copyright(c) 2017 Jeff Hotchkiss
// Licensed under the MIT License. See License.md in the project root for license information.
using Microsoft.AspNetCore.DataProtection.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace AspNetCore.DataProtection.Aws.IntegrationTests
{
    /// <summary>
    /// Borrowed straight from https://github.com/aspnet/DataProtection/blob/master/src/Microsoft.AspNetCore.DataProtection/Repositories/EphemeralXmlRepository.cs
    /// since Microsoft made this internal, which makes external testing that much harder
    /// </summary>
    internal class EphemeralXmlRepository : IXmlRepository
    {
        private readonly List<XElement> storedElements = new List<XElement>();

        public virtual IReadOnlyCollection<XElement> GetAllElements()
        {
            // force complete enumeration under lock for thread safety
            lock (storedElements)
            {
                return GetAllElementsCore().ToList().AsReadOnly();
            }
        }

        private IEnumerable<XElement> GetAllElementsCore()
        {
            // this method must be called under lock
            foreach (var element in storedElements)
            {
                yield return new XElement(element); // makes a deep copy so caller doesn't inadvertently modify it
            }
        }

        public virtual void StoreElement(XElement element, string friendlyName)
        {
            if (element == null)
            {
                throw new ArgumentNullException(nameof(element));
            }

            var cloned = new XElement(element); // makes a deep copy so caller doesn't inadvertently modify it

            // under lock for thread safety
            lock (storedElements)
            {
                storedElements.Add(cloned);
            }
        }
    }
}
