// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0.
//
// Copied verbatim as a useful testing internal implementation detail
using Microsoft.AspNet.DataProtection.Repositories;
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
        private readonly List<XElement> _storedElements = new List<XElement>();

        public virtual IReadOnlyCollection<XElement> GetAllElements()
        {
            // force complete enumeration under lock for thread safety
            lock (_storedElements)
            {
                return GetAllElementsCore().ToList().AsReadOnly();
            }
        }

        private IEnumerable<XElement> GetAllElementsCore()
        {
            // this method must be called under lock
            foreach (XElement element in _storedElements)
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

            XElement cloned = new XElement(element); // makes a deep copy so caller doesn't inadvertently modify it

            // under lock for thread safety
            lock (_storedElements)
            {
                _storedElements.Add(cloned);
            }
        }
    }
}
