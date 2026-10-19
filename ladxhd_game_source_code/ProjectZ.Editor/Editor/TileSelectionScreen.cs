using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using ProjectZ.Base;
using ProjectZ.InGame.Graphics;
using ProjectZ.InGame.Map;
using ProjectZ.InGame.Things;

namespace ProjectZ.Editor
{
    internal class TileSelectionScreen
    {
        private readonly EditorCamera _camera = new EditorCamera();

        private TileMap _tileMap;
        public int[,] SelectedTiles;
        
        private string _currentMapFileName;

        private int _currentSelection;
        private int _selectionEnd;
        private int _selectionStart;

        private bool _selecting;

        public void Load(ContentManager content)
        {
            _camera.Scale = 5;
        }

        public void Update(GameTime gameTime)
        {
            Game1.UiManager.CurrentScreen = Values.EditorUiTileSelection;

            _tileMap = Game1.GameManager.MapManager.CurrentMap.TileMap;

            // The selection math below works in tileset pixels (game pixels) and not HD texels.
            var tilesetWidth = HdTexture.LogicalWidth(_tileMap.SprTileset);
            var tilesetHeight = HdTexture.LogicalHeight(_tileMap.SprTileset);

            // Center the tileset.
            if (Game1.GameManager.MapManager.CurrentMap.MapName != _currentMapFileName)
            {
                _currentMapFileName = Game1.GameManager.MapManager.CurrentMap.MapName;

                _camera.Location.X = (int)(Game1.WindowWidth - tilesetWidth * _camera.Scale) / 2;
                _camera.Location.Y = (int)(Game1.WindowHeight - tilesetHeight * _camera.Scale) / 2;
            }

            var mousePosition = InputHandler.MousePosition();

            // Move the tileset.
            if (!InputHandler.MouseMiddleStart() && InputHandler.MouseMiddleDown())
                _camera.Location += mousePosition - InputHandler.LastMousePosition();

            // Update tileset scale.
            if (InputHandler.MouseWheelUp())
                _camera.Zoom(1, mousePosition);
            if (InputHandler.MouseWheelDown())
                _camera.Zoom(-1, mousePosition);

            // Clamp the position of the tileset to stay inside the _camera.Scale.
            var minVisible = 48;
            _camera.Location.X = (int)MathHelper.Clamp(_camera.Location.X,
                -tilesetWidth * _camera.Scale + minVisible * _camera.Scale,
                Game1.WindowWidth - minVisible * _camera.Scale);
            _camera.Location.Y = (int)MathHelper.Clamp(_camera.Location.Y,
                -tilesetHeight * _camera.Scale + minVisible * _camera.Scale,
                Game1.WindowHeight - minVisible * _camera.Scale);

            // Update the current selection.
            if (InputHandler.MouseIntersect(new Rectangle(
                _camera.Location.X, _camera.Location.Y,
                (int)(tilesetWidth * _camera.Scale),
                (int)(tilesetHeight * _camera.Scale))))
            {
                _currentSelection =
                    (mousePosition.X - _camera.Location.X) / (int)(_tileMap.AtlasTileSize * _camera.Scale) % _tileMap.TileCountX +
                    (mousePosition.Y - _camera.Location.Y) / (int)(_tileMap.AtlasTileSize * _camera.Scale) * _tileMap.TileCountX;

                _selectionEnd = _currentSelection;
            }
            else
                _currentSelection = -1;

            // Select a tile.
            if (InputHandler.MouseLeftStart() && _currentSelection != -1)
            {
                _selecting = true;
                _selectionStart = _currentSelection;
            }

            if (InputHandler.MouseLeftReleased() && _selecting)
            {
                _selecting = false;

                // Select multiple tiles.
                var start = Math.Min(_selectionStart, _selectionEnd);
                var end = Math.Max(_selectionStart, _selectionEnd);
                SelectedTiles = new int[Math.Abs(
                    end % _tileMap.TileCountX - start % _tileMap.TileCountX) + 1,
                    end / _tileMap.TileCountX - start / _tileMap.TileCountX + 1];

                for (var y = start / _tileMap.TileCountX; y <= end / _tileMap.TileCountX; y++)
                    for (var x = Math.Min(start % _tileMap.TileCountX, end % _tileMap.TileCountX);
                        x <= Math.Max(start % _tileMap.TileCountX, end % _tileMap.TileCountX); x++)
                    {
                        SelectedTiles[
                            x - Math.Min(start % _tileMap.TileCountX, end % _tileMap.TileCountX),
                            y - start / _tileMap.TileCountX] = x + y * _tileMap.TileCountX;
                    }
            }
        }
        public void Draw(SpriteBatch spriteBatch, bool blurTileset)
        {
            spriteBatch.Begin(SpriteSortMode.Deferred, null, SamplerState.PointWrap, null, null, null, _camera.TransformMatrix);

            var tileset = blurTileset ? _tileMap.SprTilesetBlur : _tileMap.SprTileset;
            var tilesetWidth = HdTexture.LogicalWidth(tileset);
            var tilesetHeight = HdTexture.LogicalHeight(tileset);

            // Draw the tiled background.
            spriteBatch.Draw(Resources.SprTiledBlock, Vector2.Zero,
                new Rectangle(0, 0,
                (tilesetWidth / _tileMap.TileSize) * 16,
                (tilesetHeight / _tileMap.TileSize) * 16), Color.White);

            // Draw the tileset at its logical size so the selection overlays line up with HD tilesets.
            spriteBatch.Draw(tileset, Vector2.Zero, new Rectangle(0, 0, tilesetWidth, tilesetHeight), Color.White);

            // Draw the current selection.
            if (_currentSelection >= 0)
                spriteBatch.Draw(Resources.SprWhite, new Rectangle(
                    _currentSelection % _tileMap.TileCountX * _tileMap.AtlasTileSize,
                    _currentSelection / _tileMap.TileCountX * _tileMap.AtlasTileSize,
                    _tileMap.AtlasTileSize, _tileMap.AtlasTileSize), Color.White * 0.5f);

            // Draw the selection.
            if (SelectedTiles != null)
            {
                for (var y = 0; y < SelectedTiles.GetLength(1); y++)
                    for (var x = 0; x < SelectedTiles.GetLength(0); x++)
                    {
                        if (SelectedTiles[x, y] >= 0)
                            spriteBatch.Draw(Resources.SprWhite, new Rectangle(
                                (SelectedTiles[x, y] % _tileMap.TileCountX) * _tileMap.AtlasTileSize,
                                (SelectedTiles[x, y] / _tileMap.TileCountX) * _tileMap.AtlasTileSize,
                                _tileMap.AtlasTileSize, _tileMap.AtlasTileSize), Color.Red * 0.5f);
                    }
            }

            if (_selecting)
            {
                spriteBatch.Draw(Resources.SprWhite, new Rectangle(
                    Math.Min(_selectionStart % _tileMap.TileCountX, _selectionEnd % _tileMap.TileCountX) * _tileMap.AtlasTileSize,
                    Math.Min(_selectionStart / _tileMap.TileCountX, _selectionEnd / _tileMap.TileCountX) * _tileMap.AtlasTileSize,
                    (Math.Abs(_selectionStart % _tileMap.TileCountX - _selectionEnd % _tileMap.TileCountX) + 1) * _tileMap.AtlasTileSize,
                    (Math.Abs(_selectionStart / _tileMap.TileCountX - _selectionEnd / _tileMap.TileCountX) + 1) * _tileMap.AtlasTileSize), Color.PaleVioletRed * 0.5f);
            }

            spriteBatch.End();
        }
    }
}
