﻿using System;
using Takenet.Elephant.Sql;
using Takenet.Elephant.Sql.Mapping;

namespace Takenet.Elephant.Tests.Sql
{
    public abstract class SqlGuidItemMapFacts : GuidItemMapFacts
    {
        private readonly ISqlFixture _serverFixture;

        protected SqlGuidItemMapFacts(ISqlFixture serverFixture)
        {
            _serverFixture = serverFixture;
        }

        public override IMap<Guid, Item> Create()
        {
            var table = TableBuilder
                .WithName("GuidItems")
                .WithColumnsFromTypeProperties<Item>()
                .WithKeyColumnFromType<Guid>("Key")
                .Build();
            _serverFixture.DropTable(table.Name);

            var keyMapper = new ValueMapper<Guid>("Key");
            var valueMapper = new TypeMapper<Item>(table);
            return new SqlMap<Guid, Item>(_serverFixture.DatabaseDriver, _serverFixture.ConnectionString, table, keyMapper, valueMapper);
        }                     
    }
}
