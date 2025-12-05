-- 创建每日游戏统计数据表
CREATE TABLE IF NOT EXISTS `daily_game_statistics` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY COMMENT '自增主键',
    `statistics_date` DATE NOT NULL COMMENT '统计日期 (UTC, yyyy-MM-dd)',
    `new_registrations` INT NOT NULL DEFAULT 0 COMMENT '当天新注册玩家数',
    `active_players` INT NOT NULL DEFAULT 0 COMMENT '当天活跃玩家数 (至少登录一次)',
    `max_online_players` INT NOT NULL DEFAULT 0 COMMENT '最大在线人数',
    `avg_online_players` DECIMAL(10, 2) NOT NULL DEFAULT 0 COMMENT '平均在线人数',
    `total_players` INT NOT NULL DEFAULT 0 COMMENT '总玩家数 (累计)',
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '最后更新时间',
    UNIQUE KEY `uk_statistics_date` (`statistics_date`),
    KEY `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='每日游戏统计数据表';

-- 创建在线人数实时统计表
CREATE TABLE IF NOT EXISTS `online_players_snapshot` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY COMMENT '自增主键',
    `statistics_date` DATE NOT NULL COMMENT '统计日期 (UTC, yyyy-MM-dd)',
    `hour` INT NOT NULL COMMENT '统计时间 (小时, 0-23)',
    `online_count` INT NOT NULL COMMENT '该小时的在线人数',
    `recorded_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '记录时间',
    KEY `idx_date_hour` (`statistics_date`, `hour`),
    KEY `idx_recorded_at` (`recorded_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='在线人数实时统计表';

-- 创建玩家活动统计表
CREATE TABLE IF NOT EXISTS `player_activity_statistics` (
    `id` BIGINT AUTO_INCREMENT PRIMARY KEY COMMENT '自增主键',
    `statistics_date` DATE NOT NULL COMMENT '统计日期 (UTC, yyyy-MM-dd)',
    `total_locations_completed` INT NOT NULL DEFAULT 0 COMMENT '完成的地图点位总数',
    `total_events_completed` INT NOT NULL DEFAULT 0 COMMENT '完成的旅行事件总数',
    `total_distance_meters` DECIMAL(15, 2) NOT NULL DEFAULT 0 COMMENT '总跑步距离 (米)',
    `avg_player_level` DECIMAL(10, 2) NOT NULL DEFAULT 0 COMMENT '平均玩家等级',
    `created_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '创建时间',
    `updated_at` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP COMMENT '最后更新时间',
    UNIQUE KEY `uk_activity_date` (`statistics_date`),
    KEY `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='玩家活动统计表';

