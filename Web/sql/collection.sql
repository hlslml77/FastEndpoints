-- Collections feature schema (MySQL)

CREATE TABLE IF NOT EXISTS `player_collection` (
  `user_id` BIGINT NOT NULL,
  `collection_id` INT NOT NULL,
  `obtained_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`user_id`, `collection_id`),
  KEY `idx_player_collection_user` (`user_id`),
  KEY `idx_player_collection_collection` (`collection_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `player_collection_combo_claim` (
  `user_id` BIGINT NOT NULL,
  `combo_id` INT NOT NULL,
  `claimed_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`user_id`, `combo_id`),
  KEY `idx_player_collection_combo_user` (`user_id`),
  KEY `idx_player_collection_combo_combo` (`combo_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Global cap counter for LimitedEditionCollectibles
CREATE TABLE IF NOT EXISTS `collection_global_counter` (
  `collection_id` INT NOT NULL,
  `total_obtained` INT NOT NULL DEFAULT 0,
  `updated_at` DATETIME(6) NOT NULL,
  PRIMARY KEY (`collection_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

