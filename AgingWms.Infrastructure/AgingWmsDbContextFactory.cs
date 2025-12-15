using AgingWms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace AgingWms.Infrastructure
{
    public class AgingWmsDbContextFactory : IDesignTimeDbContextFactory<AgingWmsDbContext>
    {
        public AgingWmsDbContext CreateDbContext(string[] args)
        {
            // =========================================================
            // 1. 智能定位 appsettings.json 路径
            // =========================================================
            var currentDir = Directory.GetCurrentDirectory();
            var configPath = currentDir;

            // 你的 WPF 项目文件夹名称 (如果你的文件夹名不一样，请修改这里)
            string wpfProjectName = "AgingWms.Client";

            // 情况 A：你在解决方案根目录运行命令 (能看到 AgingWms.Client 子文件夹)
            if (Directory.Exists(Path.Combine(currentDir, wpfProjectName))
                && File.Exists(Path.Combine(currentDir, wpfProjectName, "appsettings.json")))
            {
                configPath = Path.Combine(currentDir, wpfProjectName);
            }
            // 情况 B：你在 Infrastructure 目录运行命令 (AgingWms.Client 在上一级的隔壁)
            else if (File.Exists(Path.Combine(currentDir, "..", wpfProjectName, "appsettings.json")))
            {
                configPath = Path.GetFullPath(Path.Combine(currentDir, "..", wpfProjectName));
            }

            // =========================================================
            // 2. 读取配置
            // =========================================================
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(configPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // =========================================================
            // 3. 获取连接字符串 (跟 WPF 保持绝对统一)
            // =========================================================
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            // 安全检查：防止名字写错导致连到空库
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    $"无法在路径 [{configPath}] 的 appsettings.json 中找到名为 'DefaultConnection' 的连接字符串。\n" +
                    $"请检查 JSON 文件中的 ConnectionStrings 节点名称是否匹配！");
            }

            // =========================================================
            // 4. 创建 Context
            // =========================================================
            var optionsBuilder = new DbContextOptionsBuilder<AgingWmsDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new AgingWmsDbContext(optionsBuilder.Options);
        }
    }
}