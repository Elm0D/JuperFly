﻿using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class MapGenerator : MonoBehaviour
{
    public int width;
    public int height;
    public int borderSizeOnMap = 1;
    public int WallThresholdSize = 50;
    public int RoomThresholdSize = 50;

    public string seed;
    public bool useRandomSeed;

    [Range(0,100)]
    public int randomFillPercent;

    int[,] map;

    void Start()
    {
        GenerateMap();

    }

    void Update()
    {
        if( Input.GetMouseButtonDown( 0 ) )
        {
            GenerateMap();
        }
    }

    void GenerateMap()
    {
        map = new int[width, height];
        RandomFillMap();

        for( int i = 0; i < 5; i++ )
        {
            SmoothMap();
        }

        ProcessMap();

        // Для изменения отступа от края карты
        int borderSize = borderSizeOnMap;
        int[,] borderedMap = new int[width + borderSize * 2, height + borderSize * 2];

        for( int x = 0; x < borderedMap.GetLength( 0 ); x++ )
        {
            for( int y = 0; y < borderedMap.GetLength( 1 ); y++ )
            {
                if( x >= borderSize && x < width + borderSize && y >= borderSize && y < height + borderSize )
                {
                    borderedMap[x, y] = map[x - borderSize, y - borderSize];
                } else
                {
                    borderedMap[x, y] = 1;
                }
            }
        }
        
        MeshGenerator meshGen = GetComponent<MeshGenerator>();
        meshGen.GenerateMesh( borderedMap, 1 );
    }

    void ProcessMap()
    {
        List<List<Coord>> wallRegions = GetRegions( 1 );
        int wallThresholdSize = WallThresholdSize; //порог размера  стены 50

        foreach( List<Coord> wallRegion in wallRegions )
        {
            if( wallRegion.Count < wallThresholdSize )
            {
                foreach( Coord tile in wallRegion )
                {
                    map[tile.tileX, tile.tileY] = 0;
                }

            }
        }

        List<List<Coord>> roomRegions = GetRegions( 0 );
        int roomThresholdSize = RoomThresholdSize; //порог размера комнаты

        List<Room> survivingRooms = new List<Room>();

        foreach( List<Coord> roomRegion in roomRegions )
        {
            if( roomRegion.Count < roomThresholdSize )
            {
                foreach( Coord tile in roomRegion )
                {
                    map[tile.tileX, tile.tileY] = 1;
                }

            } else
            {
                survivingRooms.Add( new Room( roomRegion, map ) );
            }
        }
        survivingRooms.Sort();
        survivingRooms[0].isMainRoom = true;
        survivingRooms[0].isAccessibleFromMainRoom = true;

        ConnectedClosestRoom( survivingRooms );
    }

    void ConnectedClosestRoom( List<Room> allRooms, bool forceAccessibilityFromMainRoom = false )
    {
        List<Room> roomListA = new List<Room>();
        List<Room> roomListB = new List<Room>();

        if( forceAccessibilityFromMainRoom )
        {
            foreach( Room room in allRooms )
            {
                if( room.isAccessibleFromMainRoom )
                {
                    roomListB.Add( room );
                } else
                {
                    roomListA.Add( room );
                }
            }
        } else
        {
            roomListA = allRooms;
            roomListB = allRooms;
        }


        int bestDistance = 0;
        Coord bestTileA = new Coord();
        Coord bestTileB = new Coord();
        Room bestRoomA = new Room();
        Room bestRoomB = new Room();
        bool possibleConnectionFound = false;

        foreach( Room roomA in roomListA )
        {
            if( !forceAccessibilityFromMainRoom )
            {
                possibleConnectionFound = false;
                if( roomA.conectedRooms.Count > 0 )
                {
                    continue;
                }
            }
            foreach( Room roomB in roomListB )
            {
                if( roomA == roomB || roomA.isConnected( roomB ) )
                { continue; }

                for( int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA++ )
                {
                    for( int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB++ )
                    {
                        Coord tileA = roomA.edgeTiles[tileIndexA];
                        Coord tileB = roomB.edgeTiles[tileIndexB];
                        int distanceBetweenRooms =
                            (int)(Mathf.Pow( tileA.tileX - tileB.tileX, 2 ) + Mathf.Pow( tileA.tileY - tileB.tileY, 2 ));

                        if( distanceBetweenRooms < bestDistance || !possibleConnectionFound )
                        {
                            bestDistance = distanceBetweenRooms;
                            possibleConnectionFound = true;
                            bestTileA = tileA;
                            bestTileB = tileB;
                            bestRoomA = roomA;
                            bestRoomB = roomB;
                        }
                    }
                }
            }

            if( possibleConnectionFound && !forceAccessibilityFromMainRoom )
            {
                CreatePassage( bestRoomA, bestRoomB, bestTileA, bestTileB );
            }
        }

        if( possibleConnectionFound && forceAccessibilityFromMainRoom )
        {
            CreatePassage( bestRoomA, bestRoomB, bestTileA, bestTileB );
            ConnectedClosestRoom( allRooms, true );
        }

        if( !forceAccessibilityFromMainRoom )
        {
            ConnectedClosestRoom( allRooms, true );
        }
    }



    void CreatePassage( Room roomA, Room roomB, Coord tileA, Coord tileB )
    {
        Room.ConnectRooms( roomA, roomB );
        Debug.DrawLine( CoordToWorldPoint( tileA ), CoordToWorldPoint( tileB ), Color.green, 5 );

        List<Coord> line = GetLine( tileA, tileB );
        foreach( Coord c in line )
        {
            DrawCircle( c, 5 );// Здесь изменяется размер соеденяющих коридоров.
        }
    }

    void DrawCircle( Coord c, int r )
    {
        for( int x = -r; x <= r; x++ )
        {
            for( int y = -r; y <= r; y++ )
            {
                if( x * x + y * y <= r * r )
                {
                    int drawX = c.tileX + x;
                    int drawY = c.tileY + y;
                    if( IsInMapRange( drawX, drawY ) )
                    {
                        map[drawX, drawY] = 0;
                    }
                }
            }
        }
    }

    List<Coord> GetLine( Coord from, Coord to )
    {
        List<Coord> line = new List<Coord>();

        int x = from.tileX;
        int y = from.tileY;

        int dx = to.tileX - from.tileX;
        int dy = to.tileY - from.tileY;

        bool inverted = false;
        int step = Math.Sign( dx );
        int gradientStep = Math.Sign( dy );

        int longest = Mathf.Abs( dx );
        int shortest = Mathf.Abs( dy );

        if( longest < shortest )
        {
            inverted = true;
            longest = Mathf.Abs( dy );
            shortest = Mathf.Abs( dx );

            step = Math.Sign( dy );
            gradientStep = Math.Sign( dx );
        }

        int gradientAccumulation = longest / 2;

        for( int i = 0; i < longest; i++ )
        {
            line.Add( new Coord( x, y ) );
            if( inverted )
            {
                y += step;
            } else
            {
                x += step;
            }
            gradientAccumulation += shortest;
            if( gradientAccumulation >= longest )
            {
                if( inverted )
                {
                    x += gradientStep;
                } else
                {
                    y += gradientStep;
                }
                gradientAccumulation -= longest;
            }
        }
        return line;
    }

    Vector3 CoordToWorldPoint( Coord tile )
    {
        return new Vector3( -width / 2 + .5f + tile.tileX, 2, -height / 2 + .5f + tile.tileY );
    }

    List<List<Coord>> GetRegions( int tileType )
    {
        List<List<Coord>> regions = new List<List<Coord>>();
        int[,] mapFlags = new int[width, height];
        for( int x = 0; x < width; x++ )
        {
            for( int y = 0; y < height; y++ )
            {
                if( mapFlags[x, y] == 0 && map[x, y] == tileType )
                {
                    List<Coord> newRegion = GetRegionTiles( x, y );
                    regions.Add( newRegion );
                    foreach( Coord tile in newRegion )
                    {
                        mapFlags[tile.tileX, tile.tileY] = 1;
                    }
                }
            }
        }
        return regions;
    }

    List<Coord> GetRegionTiles( int statX, int statY )
    {
        List<Coord> tiles = new List<Coord>();
        int[,] mapFlags = new int[width, height];
        int tileType = map[statX, statY];

        Queue<Coord> queue = new Queue<Coord>();
        queue.Enqueue( new Coord( statX, statY ) );
        mapFlags[statX, statY] = 1;

        while( queue.Count > 0 )
        {
            Coord tile = queue.Dequeue();
            tiles.Add( tile );

            for( int x = tile.tileX - 1; x <= tile.tileX + 1; x++ )
            {
                for( int y = tile.tileY - 1; y <= tile.tileY + 1; y++ )
                {
                    if( IsInMapRange( x, y ) && (y == tile.tileY || x == tile.tileX) )
                    {
                        if( mapFlags[x, y] == 0 && map[x, y] == tileType )
                        {
                            mapFlags[x, y] = 1;
                            queue.Enqueue( new Coord( x, y ) );
                        }
                    }
                }
            }
        }
        return tiles;
    }

    bool IsInMapRange( int x, int y )
    {
        return x >= 0 && x < width && y >= 0 && y < height;
    }

    void RandomFillMap()
    {
        if( useRandomSeed )
            seed = Time.time.ToString();

        System.Random pseudoDoRandom = new System.Random( seed.GetHashCode() );

        for( int x = 0; x < width; x++ )
        {
            for( int y = 0; y < height; y++ )
            {
                if( x == 0 || x == width - 1 || y == 0 || y == height - 1 )
                {
                    map[x, y] = 1;
                } else
                {
                    map[x, y] = (pseudoDoRandom.Next( 0, 100 ) < randomFillPercent) ? 1 : 0;
                }
            }
        }

    }
    void SmoothMap()
    {
        for( int x = 0; x < width; x++ )
        {
            for( int y = 0; y < height; y++ )
            {
                int neighbourTiles = GetSurroundingWallCount( x, y );

                if( neighbourTiles > 4 )
                    map[x, y] = 1;
                else if( neighbourTiles < 4 )
                    map[x, y] = 0;
            }
        }
    }

    int GetSurroundingWallCount( int gridX, int gridY )
    {
        int wallCount = 0;
        for( int neightbourX = gridX - 1; neightbourX <= gridX + 1; neightbourX++ )
        {
            for( int neightbourY = gridY - 1; neightbourY <= gridY + 1; neightbourY++ )
            {
                if( IsInMapRange( neightbourX, neightbourY ) )
                {
                    if( neightbourX != gridX || neightbourY != gridY )
                    {
                        wallCount += map[neightbourX, neightbourY];
                    }
                } else
                {
                    wallCount++;
                }
            }
        }

        return wallCount;
    }

    struct Coord
    {
        public int tileX;
        public int tileY;
        public Coord( int x, int y )
        {
            tileX = x;
            tileY = y;

        }
    }

    class Room : IComparable<Room>
    {
        public List<Coord> tiles;
        public List<Coord> edgeTiles;
        public List<Room> conectedRooms;
        public int roomSize;
        public bool isAccessibleFromMainRoom;
        public bool isMainRoom;

        public Room()
        {

        }

        public Room( List<Coord> roomTiles, int[,] map )
        {
            tiles = roomTiles;
            roomSize = tiles.Count;
            conectedRooms = new List<Room>();
            edgeTiles = new List<Coord>();
            foreach( Coord tile in tiles )
            {
                for( int x = tile.tileX - 1; x <= tile.tileX + 1; x++ )
                {
                    for( int y = tile.tileY - 1; y <= tile.tileY + 1; y++ )
                    {
                        if( x == tile.tileX || y == tile.tileY )
                        {
                            if( map[x, y] == 1 )
                            {
                                edgeTiles.Add( tile );
                            }
                        }
                    }
                }
            }
        }

        public void setAccessibleFromMainRoom()
        {
            if( !isAccessibleFromMainRoom )
            {
                isAccessibleFromMainRoom = true;
                foreach( Room connectedRoom in conectedRooms )
                {
                    connectedRoom.setAccessibleFromMainRoom();
                }
            }
        }

        public static void ConnectRooms( Room roomA, Room roomB )
        {
            if( roomA.isAccessibleFromMainRoom )
            {
                roomB.setAccessibleFromMainRoom();
            } else if( roomB.isAccessibleFromMainRoom )
            {
                roomA.setAccessibleFromMainRoom();
            }
            roomA.conectedRooms.Add( roomB );
            roomB.conectedRooms.Add( roomA );
        }

        public bool isConnected( Room otherRoom )
        {
            return conectedRooms.Contains( otherRoom );
        }

        public int CompareTo( Room otherRoom )
        {
            return otherRoom.roomSize.CompareTo( roomSize );
        }
    }

}
