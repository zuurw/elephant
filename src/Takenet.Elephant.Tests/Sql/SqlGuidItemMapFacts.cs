﻿using System;
using Takenet.Elephant.Sql;
using Takenet.Elephant.Sql.Mapping;
using Xunit;

namespace Takenet.Elephant.Tests.Sql
{
    [Collection("Sql")]
    public class SqlGuidItemMapFacts : GuidItemMapFacts
    {
        private readonly SqlConnectionFixture _fixture;

        public SqlGuidItemMapFacts(SqlConnectionFixture fixture)
        {
            _fixture = fixture;
        }

        public override IMap<Guid, Item> Create()
        {
            var databaseDriver = new SqlDatabaseDriver();
            var table = TableBuilder
                .WithName("GuidItems")
                .WithColumnsFromTypeProperties<Item>()
                .WithKeyColumnFromType<Guid>("Key")
                .Build();
            _fixture.DropTable(table.Name);

            var keyMapper = new ValueMapper<Guid>("Key");
            var valueMapper = new TypeMapper<Item>(table);
            return new SqlMap<Guid, Item>(databaseDriver, _fixture.ConnectionString, table, keyMapper, valueMapper);
        }                     
    }
}