using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XamlNavigator.Models;

namespace XamlNavigator.DynamicPages
{
    public interface IDynamicPageRepository
    {
        Task<List<PageDefinitionModel>> GetPagesAsync();
        Task<PageDefinitionModel> GetPageAsync(long pageId);
        Task<long> InsertPageAsync(PageDefinitionModel page);
        Task UpdatePageAsync(PageDefinitionModel page);
        Task DeletePageAsync(long pageId);
        Task EnsureTableAsync(string tableName, Type modelType);
        Task<List<object>> GetRowsAsync(string tableName, Type modelType);
        Task<long> InsertRowAsync(string tableName, Type modelType, object item);
        Task UpdateRowAsync(string tableName, Type modelType, object item);
        Task<bool> DeleteRowAsync(string tableName, long id);
        Task<List<FieldDefinitionModel>> GetFieldsAsync(long pageId);
        Task SaveFieldsAsync(long pageId, List<FieldDefinitionModel> defs);
        Task DropTableAsync(string tableName);
        Type ResolveModelType(string typeName);
    }
}