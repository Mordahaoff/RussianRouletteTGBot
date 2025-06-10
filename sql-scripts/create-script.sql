drop schema public cascade;
create schema public;
grant all on schema public to admin;
grant all on schema public to public;

create table Users(
ID_User serial primary key,
TG_ID bigint not null,
Bot_State_ID integer not null default 1,
Max_score integer not null default 0,
Score integer not null default 0
);

create table Money_Bonuses(
ID_Money_Bonus serial primary key,
User_ID integer references Users(ID_User) on delete cascade not null,
Collection_Time timestamp not null default current_date
);

create table Achievements(
ID_Achievement serial primary key,
Title varchar(255) not null,
Description text not null
);

create table User_Achievements(
ID_User_Achievement serial primary key,
User_ID integer references Users(ID_User) on delete cascade not null,
Achievement_ID integer references Achievements(ID_Achievement) on delete cascade not null,
Date_Received date not null
);

create table Types_of_Bullet(
ID_Type_of_Bullet serial primary key,
Title varchar(255) not null,
Multiplier numeric(3,2) not null,
Price smallint not null
);

create table Settings(
ID_Setting serial primary key,
User_ID integer references Users(ID_User) on delete cascade not null,
Type_of_Bullet_ID integer references Types_of_Bullet(ID_Type_of_Bullet) on delete cascade default 1 not null,
Count_of_Bullets smallint default 1 not null
);

create table Results_of_Game(
ID_Result_of_Game serial primary key,
Title varchar(255) not null
);

create table Games(
ID_Game serial primary key,
User_ID integer references Users(ID_User) on delete cascade not null,
Settings_ID integer references Settings(ID_Setting) on delete cascade not null,
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