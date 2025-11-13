using MySqlConnector;
using System;
using System.Threading.Tasks;

class Program
{
    static async Task Main(string[] args)
    {
        var connectionString = "Server=localhost;Port=3306;Database=pitpatworldmvp;Uid=root;Pwd=123456;";
        
        Console.WriteLine("========================================");
        Console.WriteLine("检查数据库数据");
        Console.WriteLine("========================================\n");

        try
        {
            await using var connection = new MySqlConnection(connectionString);
            await connection.OpenAsync();
            Console.WriteLine("✓ 数据库连接成功\n");

            // 1. 检查 player_role_growth 表
            Console.WriteLine("1. 玩家角色成长数据 (player_role_growth):");
            Console.WriteLine("   查询条件: user_id = 1");
            Console.WriteLine("   " + new string('-', 100));
            
            var query1 = @"SELECT user_id, current_level, current_experience, 
                          attr_upper_limb, attr_lower_limb, attr_core, attr_heart_lungs,
                          today_attribute_points, last_update_time 
                          FROM player_role_growth WHERE user_id = 1";
            
            await using (var cmd = new MySqlCommand(query1, connection))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    Console.WriteLine($"   用户ID: {reader.GetInt64(0)}");
                    Console.WriteLine($"   当前等级: {reader.GetInt32(1)}");
                    Console.WriteLine($"   当前经验: {reader.GetInt32(2)}");
                    Console.WriteLine($"   上肢属性: {reader.GetInt32(3)}");
                    Console.WriteLine($"   下肢属性: {reader.GetInt32(4)}");
                    Console.WriteLine($"   核心属性: {reader.GetInt32(5)}");
                    Console.WriteLine($"   心肺属性: {reader.GetInt32(6)}");
                    Console.WriteLine($"   今日属性点: {reader.GetInt32(7)}");
                    Console.WriteLine($"   最后更新时间: {reader.GetDateTime(8):yyyy-MM-dd HH:mm:ss}");
                }
                else
                {
                    Console.WriteLine("   ✗ 未找到数据");
                }
            }

            // 2. 检查 player_map_progress 表
            Console.WriteLine("\n2. 玩家地图进度记录 (player_map_progress):");
            Console.WriteLine("   查询条件: user_id = 1, 最近5条记录");
            Console.WriteLine("   " + new string('-', 100));
            
            var query2 = @"SELECT id, user_id, start_location_id, end_location_id, 
                          distance_meters, created_at 
                          FROM player_map_progress 
                          WHERE user_id = 1 
                          ORDER BY created_at DESC LIMIT 5";
            
            await using (var cmd = new MySqlCommand(query2, connection))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                int count = 0;
                while (await reader.ReadAsync())
                {
                    count++;
                    Console.WriteLine($"\n   记录 #{count}:");
                    Console.WriteLine($"   ID: {reader.GetInt64(0)}");
                    Console.WriteLine($"   用户ID: {reader.GetInt64(1)}");
                    Console.WriteLine($"   起点位置ID: {reader.GetInt32(2)}");
                    Console.WriteLine($"   终点位置ID: {reader.GetInt32(3)}");
                    Console.WriteLine($"   距离(米): {reader.GetDecimal(4)}");
                    Console.WriteLine($"   创建时间: {reader.GetDateTime(5):yyyy-MM-dd HH:mm:ss}");
                }
                
                if (count == 0)
                {
                    Console.WriteLine("   ✗ 未找到数据");
                }
                else
                {
                    Console.WriteLine($"\n   共找到 {count} 条记录");
                }
            }

            // 3. 检查 player_map_location_visit 表
            Console.WriteLine("\n3. 玩家地图点位访问记录 (player_map_location_visit):");
            Console.WriteLine("   查询条件: user_id = 1");
            Console.WriteLine("   " + new string('-', 100));
            
            var query3 = @"SELECT user_id, location_id, first_visit_time, 
                          visit_count, last_visit_time 
                          FROM player_map_location_visit 
                          WHERE user_id = 1";
            
            await using (var cmd = new MySqlCommand(query3, connection))
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                int count = 0;
                while (await reader.ReadAsync())
                {
                    count++;
                    Console.WriteLine($"\n   记录 #{count}:");
                    Console.WriteLine($"   用户ID: {reader.GetInt64(0)}");
                    Console.WriteLine($"   地图点位ID: {reader.GetInt32(1)}");
                    Console.WriteLine($"   首次访问时间: {reader.GetDateTime(2):yyyy-MM-dd HH:mm:ss}");
                    Console.WriteLine($"   访问次数: {reader.GetInt32(3)}");
                    Console.WriteLine($"   最后访问时间: {reader.GetDateTime(4):yyyy-MM-dd HH:mm:ss}");
                }
                
                if (count == 0)
                {
                    Console.WriteLine("   ✗ 未找到数据");
                }
                else
                {
                    Console.WriteLine($"\n   共找到 {count} 条记录");
                }
            }

            Console.WriteLine("\n========================================");
            Console.WriteLine("✓ 数据库检查完成!");
            Console.WriteLine("========================================");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ 错误: {ex.Message}");
            Console.WriteLine($"   详细信息: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"   内部异常: {ex.InnerException.Message}");
            }
        }
    }
}

