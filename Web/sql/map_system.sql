-- 地图系统表结构
-- 创建玩家地图进度记录表
CREATE TABLE IF NOT EXISTS `player_map_progress` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `user_id` BIGINT NOT NULL COMMENT '用户ID',
  `start_location_id` INT NOT NULL COMMENT '起点位置ID',
  `end_location_id` INT NOT NULL COMMENT '终点位置ID',
  `distance_meters` DECIMAL(10, 2) NOT NULL COMMENT '跑步距离（米）',
  `created_at` DATETIME NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`),
  INDEX `idx_user_id` (`user_id`),
  INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='玩家地图进度记录表';

-- 创建玩家地图点位访问记录表
CREATE TABLE IF NOT EXISTS `player_map_location_visit` (
  `user_id` BIGINT NOT NULL COMMENT '用户ID',
  `location_id` INT NOT NULL COMMENT '地图点位ID',
  `first_visit_time` DATETIME NOT NULL COMMENT '首次访问时间',
  `visit_count` INT NOT NULL DEFAULT 1 COMMENT '访问次数',
  `last_visit_time` DATETIME NOT NULL COMMENT '最后访问时间',
  PRIMARY KEY (`user_id`, `location_id`),
  INDEX `idx_location_id` (`location_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='玩家地图点位访问记录表';

