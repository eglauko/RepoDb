﻿using System;
using RepoDb.Attributes;
using RepoDb.Enumerations;
using System.Collections.Generic;

namespace RepoDb.IntegrationTests.Models
{
    [Map("[dbo].[Customer]")]
    public class RecursiveCustomer : DataEntity
    {
        [Identity]
        public long Id { get; set; }
        public Guid GlobalId { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string Address { get; set; }
        public string Email { get; set; }
        public bool IsActive { get; set; }
        public DateTime LastUpdatedUtc { get; set; }
        [Attributes.Ignore(Command.Update | Command.Insert)]
        public DateTime DateInsertedUtc { get; set; }
        public string LastUserId { get; set; }

        /*
         * Recursive
         */

        // Child Orders
        public IEnumerable<RecursiveOrder> Orders { get; set; }
    }
}