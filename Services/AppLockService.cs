using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QLHS.Models;

namespace QLHS.Services
{
    public interface IAppLockService
    {
        Task<bool> AcquireLockAsync(IDbContextTransaction transaction, string resourceName, int timeoutSeconds = 10);
    }

    public class AppLockService : IAppLockService
    {
        private readonly QlhsContext _context;

        public AppLockService(QlhsContext context)
        {
            _context = context;
        }

        public async Task<bool> AcquireLockAsync(IDbContextTransaction transaction, string resourceName, int timeoutSeconds = 10)
        {
            var dbConnection = _context.Database.GetDbConnection();
            if (dbConnection.State != ConnectionState.Open)
            {
                await dbConnection.OpenAsync();
            }

            using var cmd = dbConnection.CreateCommand();
            cmd.Transaction = transaction.GetDbTransaction();
            cmd.CommandText = "sp_getapplock";
            cmd.CommandType = CommandType.StoredProcedure;

            var paramResource = cmd.CreateParameter();
            paramResource.ParameterName = "@Resource";
            paramResource.Value = resourceName;
            paramResource.DbType = DbType.String;
            cmd.Parameters.Add(paramResource);

            var paramLockMode = cmd.CreateParameter();
            paramLockMode.ParameterName = "@LockMode";
            paramLockMode.Value = "Exclusive";
            paramLockMode.DbType = DbType.String;
            cmd.Parameters.Add(paramLockMode);

            var paramLockOwner = cmd.CreateParameter();
            paramLockOwner.ParameterName = "@LockOwner";
            paramLockOwner.Value = "Transaction";
            paramLockOwner.DbType = DbType.String;
            cmd.Parameters.Add(paramLockOwner);

            var paramLockTimeout = cmd.CreateParameter();
            paramLockTimeout.ParameterName = "@LockTimeout";
            paramLockTimeout.Value = timeoutSeconds * 1000;
            paramLockTimeout.DbType = DbType.Int32;
            cmd.Parameters.Add(paramLockTimeout);

            var paramResult = cmd.CreateParameter();
            paramResult.ParameterName = "@Result";
            paramResult.Direction = ParameterDirection.ReturnValue;
            paramResult.DbType = DbType.Int32;
            cmd.Parameters.Add(paramResult);

            await cmd.ExecuteNonQueryAsync();

            int result = Convert.ToInt32(paramResult.Value);
            // 0: Lock was successfully granted synchronously.
            // 1: Lock was granted successfully after waiting.
            // >= 0 means success.
            return result >= 0;
        }
    }
}
