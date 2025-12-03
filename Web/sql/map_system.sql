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



-- 创建玩家已完成点位表
CREATE TABLE IF NOT EXISTS `player_completed_location` (
  `user_id` BIGINT NOT NULL COMMENT '用户ID',
  `location_id` INT NOT NULL COMMENT '地图点位ID',
  `completed_time` DATETIME NOT NULL COMMENT '完成时间',
  `next_challenge_time` DATETIME NULL COMMENT '下次可挑战时间（当该点位有资源倒计时时设置）',
  PRIMARY KEY (`user_id`, `location_id`),
  INDEX `idx_completed_time` (`completed_time`),
  INDEX `idx_next_challenge_time` (`next_challenge_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='玩家已完成点位表';


-- 创建玩家已解锁点位表
CREATE TABLE IF NOT EXISTS `player_unlocked_location` (
  `user_id` BIGINT NOT NULL COMMENT '用户ID',
  `location_id` INT NOT NULL COMMENT '地图点位ID',
  `unlocked_time` DATETIME NOT NULL COMMENT '解锁时间',
  PRIMARY KEY (`user_id`, `location_id`),
  INDEX `idx_unlocked_time` (`unlocked_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='玩家已解锁点位表';

-- 玩家每日随机事件表
CREATE TABLE IF NOT EXISTS `player_daily_random_event` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `user_id` BIGINT NOT NULL COMMENT '用户ID',
  `date` DATE NOT NULL COMMENT '日期(UTC)',
  `location_id` INT NOT NULL COMMENT '点位ID(PositioningPoint)',
  `event_id` INT NOT NULL COMMENT '事件配置ID',
  `is_completed` TINYINT(1) NOT NULL DEFAULT 0 COMMENT '是否已完成',
  `created_at` DATETIME NOT NULL COMMENT '创建时间',
  `completed_at` DATETIME NULL COMMENT '完成时间',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uniq_user_date_location` (`user_id`, `date`, `location_id`),
  INDEX `idx_user_date` (`user_id`, `date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='玩家每日随机事件表';

-- 地图点位人数统计表
CREATE TABLE IF NOT EXISTS `location_people_count` (
  `location_id` INT NOT NULL COMMENT '地图点位ID',
  `people_count` INT NOT NULL DEFAULT 0 COMMENT '当前在该点位的玩家人数',
  `last_update_time` DATETIME NOT NULL COMMENT '最后更新时间',
  PRIMARY KEY (`location_id`),
  INDEX `idx_last_update_time` (`last_update_time`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='地图点位人数统计表';

-- 为现有的player_completed_location表添加next_challenge_time列（如果不存在）
-- 注意：MySQL 不支持 ALTER TABLE ADD COLUMN IF NOT EXISTS，需要先检查列是否存在
-- 这里使用条件语句来处理
ALTER TABLE `player_completed_location` ADD COLUMN `next_challenge_time` DATETIME NULL COMMENT '下次可挑战时间（当该点位有资源倒计时时设置）';
ALTER TABLE `player_completed_location` ADD INDEX `idx_next_challenge_time` (`next_challenge_time`);

