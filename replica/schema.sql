-- KRYVON schema (SQLite via node:sqlite today; Postgres-compatible apart from the noted types).
-- Times are ISO-8601 UTC text in SQLite (timestamptz in Postgres). Ids are random text (uuid in Postgres).
-- Access rule: every query that touches user-owned rows is filtered by the session's user_id in server.js
-- (no row-level security in SQLite).

pragma foreign_keys = on;

create table if not exists users (
  id            text primary key,
  username      text not null unique collate nocase,
  email         text unique collate nocase,          -- null for guests / Google-only
  pass_hash     text,                                -- scrypt; null when no password
  pass_salt     text,
  google_sub    text unique,
  google_email  text,
  xp            integer not null default 0 check (xp >= 0),
  tier_points   integer not null default 0 check (tier_points >= 0),
  coins         integer not null default 500 check (coins >= 0),
  settings      text,                                -- JSON, ≤ 8 KB
  look          text,                                -- JSON character look (face, hair, clothes), validated by src/look.js
  created_at    text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  updated_at    text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);
create index if not exists users_tier on users (tier_points desc);

create table if not exists sessions (
  token       text primary key,                      -- 256-bit random
  user_id     text not null references users(id) on delete cascade,
  expires_at  text not null,
  created_at  text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);
create index if not exists sessions_user on sessions (user_id);
create index if not exists sessions_exp on sessions (expires_at);

create table if not exists matches (
  id            text primary key,
  map_id        text not null check (map_id in ('sham','north','heart','south')),
  mode          text not null check (mode in ('solo','duo','squad')),
  player_count  integer not null check (player_count between 1 and 100),
  winner_team   integer,
  duration_s    integer not null default 0,
  created_at    text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);
create index if not exists matches_created on matches (created_at);

-- one row per human player per match (bots are not stored)
create table if not exists match_players (
  match_id     text not null references matches(id) on delete cascade,
  user_id      text not null references users(id) on delete cascade,
  team         integer not null,
  place        integer not null check (place >= 1),
  kills        integer not null default 0,
  damage       integer not null default 0,
  survived_s   integer not null default 0,
  xp_gain      integer not null default 0,
  tp_delta     integer not null default 0,
  coins_gain   integer not null default 0,
  primary key (match_id, user_id)
);
create index if not exists match_players_user on match_players (user_id, match_id);

-- cosmetics catalogue lives in code (src/cosmetics.js); the DB stores ownership and loadout
create table if not exists user_items (
  user_id     text not null references users(id) on delete cascade,
  item_id     text not null,
  acquired_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now')),
  primary key (user_id, item_id)
);

create table if not exists user_loadout (
  user_id  text not null references users(id) on delete cascade,
  slot     text not null check (slot in ('scarf','top','pants','chute','gun')),
  item_id  text not null,
  primary key (user_id, slot)
);

-- for online squads (milestone 4); unused until the realtime server exists
create table if not exists parties (
  id         text primary key,
  leader_id  text not null references users(id) on delete cascade,
  mode       text not null default 'squad' check (mode in ('duo','squad')),
  map_id     text not null default 'sham',
  created_at text not null default (strftime('%Y-%m-%dT%H:%M:%fZ','now'))
);
create table if not exists party_members (
  party_id  text not null references parties(id) on delete cascade,
  user_id   text not null unique references users(id) on delete cascade,  -- one party per user
  ready     integer not null default 0,
  primary key (party_id, user_id)
);
