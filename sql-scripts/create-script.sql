drop schema public cascade;
create schema public;
grant all on schema public to admin;
grant all on schema public to public;

create table Users(
ID_User serial primary key,
TG_ID bigint not null,
Bot_State_ID integer not null default 1
);

create table Achievements(
ID_Achivevement serial primary key,
Title varchar(255) not null,
Description text not null
);

create table Types_of_Bullet(
ID_Type_of_Bullet serial primary key,
Title varchar(255) not null,
Multiplier numeric(3,2) not null,
Price smallint not null
);

create table Results_of_Game(
ID_Result_of_Game serial primary key,
Title varchar(255) not null
);

create table Games(
ID_Game serial primary key,
User_ID integer references Users(ID_User) on delete cascade not null,
Type_of_Bullet_ID integer references Types_of_Bullet(ID_Type_of_Bullet) on delete cascade default 1 not null,
Result_ID integer references Results_of_Game(ID_Result_of_Game) on delete cascade,
Count_of_Rounds smallint default 0,
Winning integer default 0,
Bet integer not null
);

create table Bullets_in_Game(
ID_Bullet_in_Game serial primary key,
Game_ID integer references Games(ID_Game) on delete cascade not null,
Index_of_Bullet smallint not null
);