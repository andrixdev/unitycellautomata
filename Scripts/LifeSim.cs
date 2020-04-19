using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Experimental.VFX;
using UnityEngine.VFX;

// Note: GameObject with this script needs to also have a VisualEffect component
public class LifeSim : MonoBehaviour
{
	// Cell Automata mechanics concerns
	private const byte DEAD = 0;
	private const byte ALIVE = 1;

	private const byte Moore = 0;
	private const byte VNeumann = 1;

	public int nbrOfCells = 20;
	private float cellLength = 1.0f;// Not used atm
	public bool torusShape;

	// Containers
	private Cell[,,] cellGrid;
	public byte[,,] nextStatusGrid;

	private int step = 0;
	public int stepInterval = 20;
	//private float timer;
	//public float pauseDuration;

	private int population = 0;

	// Automaton parameters
	public string ruleKey = "9-20/5-7,12-13,15-16/5/M";
	private string oldRuleKey;
	private byte neighbour;
	private byte[][] rule;

	public double mortality = 0.0;
	public double spawnRate = 0.05;
	public int lifeRange = 50;
	public bool fillInit = false;
	public bool centerInit = false;


	// Visualization concerns
	private Texture2D positionTexture; // Texture to store positions
    private Texture2D statusTexture; // Texture to store statuses
	private VisualEffect VFX;
	
	// MonoBehaviour Start() called once
	public void Start()
	{
		this.StartAutomaton();
		this.StartVisualsInjection();
		UnityEngine.Debug.Log(this.ruleKey);
	}
	
	// MonoBehaviour Update() called each frame
	public void Update()
	{
		// Update step (quick&dirty way)
		step++;
		if (!this.oldRuleKey.Equals(this.ruleKey))
			this.translateRule();

		if (step % stepInterval == 0)
		{
			// Update cellGrid array
			this.nextStep();
			//UnityEngine.Debug.Log(this.nextStatusGrid[this.nbrOfCells/2-2, this.nbrOfCells/2+2, this.nbrOfCells/2-3]);
			UnityEngine.Debug.Log(String.Format("Population : {0}\n",this.population));
		}
	}
	
	public void StartAutomaton()
	{
		// Init rule array (27x2) -> Change to 27... we can have between 0 and 26 alive neighours, so 27 possibilities right?
		this.rule = new byte[27][] ; // 27 values
		for(int i = 0; i < 27; i++)
        {
			this.rule[i] = new byte[2];
        }

		Cell.lifeRange = this.lifeRange;

		// Init cellGrid and nextStatusGrid
		this.cellGrid = new Cell[this.nbrOfCells,this.nbrOfCells,this.nbrOfCells];
		this.nextStatusGrid = new byte[this.nbrOfCells,this.nbrOfCells,this.nbrOfCells];
		byte initCellStatus;
		
		for(int i = 0; i < this.nbrOfCells; i++)
        {
			for(int j = 0; j < this.nbrOfCells; j++)
            {
				for(int k = 0; k < this.nbrOfCells; k++)
                {
					// Cell random initial status
					if (this.centerInit)
                    {
						if (i >= this.nbrOfCells/2 - 3 && i <= this.nbrOfCells/2 + 3 
							&& j >= this.nbrOfCells / 2 - 3 && j <= this.nbrOfCells / 2 + 3 
							&& k >= this.nbrOfCells / 2 - 3 && k <= this.nbrOfCells / 2 + 3)
                        {
							if (this.fillInit)
							{
								initCellStatus = UnityEngine.Random.value < this.spawnRate ? DEAD : ALIVE;
							}

                            else
                            {
								initCellStatus = UnityEngine.Random.value < this.spawnRate ? ALIVE : DEAD;
							}
								
                        }
                        else
                        {
							initCellStatus = DEAD;
                        }
                    }
                    else
                    {
						if (this.fillInit)
						{
							initCellStatus = UnityEngine.Random.value < this.spawnRate ? DEAD : ALIVE;
						}

						else
						{
							initCellStatus = UnityEngine.Random.value < this.spawnRate ? ALIVE : DEAD;
						}
					}

					if (initCellStatus == ALIVE)
						this.population++;

					this.cellGrid[i,j,k] = new Cell(i, j, k, this.cellLength, initCellStatus,this.mortality);
					
					// Next status grid initial value
					this.nextStatusGrid[i,j,k] = initCellStatus;
                }
            }
        }

		this.oldRuleKey = (string) this.ruleKey.Clone();
		this.translateRule();
	}
	
	public void StartVisualsInjection()
	{
		// Build textures
        CreateTextures(nbrOfCells);
		
		// Feed le VFX
		VFX = (VisualEffect) GetComponent<VisualEffect>();
		VFX.SetTexture("PositionTexture", positionTexture);
		VFX.SetTexture("StatusTexture", statusTexture);
		VFX.SetInt("LifeRange", this.lifeRange);
	}

	// Update both data grid and textures (visuals)
	private void nextStep()
	{
		this.buildNextGrid();
		int newCellLifespan;
		byte newCellStatus;
		
		for (int x = 0; x < this.nbrOfCells; x++)
		{
			for (int y = 0; y < this.nbrOfCells; y++)
			{
				for (int z = 0; z < this.nbrOfCells; z++)
				{
					newCellStatus = this.nextStatusGrid[x,y,z];
					
					// Update automaton status
					this.cellGrid[x,y,z].changeStatus(newCellStatus);
					
					// Update status texture (visuals) and lifespan
					newCellLifespan = this.cellGrid[x,y,z].getLifespan();
					
					statusTexture.SetPixel(x + nbrOfCells * y, z, new Color(newCellStatus, newCellLifespan, 0, 0));
				}
			}
		}

		// Update textures
		statusTexture.Apply();
	}
	
	private void translateRule()
	{ // Transforme la chaine ruleKey en une rule utilisable
	  // Il y a 27*2 cas (de 0 à 27 voisins vivants, selon son propre status) qui doivent être couverts par une règle au pire
	  // (Il y a plus de voisins dans un voisinage de Moore que dans un voisinage de Von Neumann)

		//Initialisation du tableau :
		for(int i = 0; i < 27; i++)
        {
			this.rule[i][0] = DEAD;
			this.rule[i][1] = DEAD;
        }

		string[] splittedRule = this.ruleKey.Split('/');
		string[] sustains = splittedRule[0].Split(',');
		string[] births = splittedRule[1].Split(',');
		string[] overpops = splittedRule[2].Split(',');
		string neighbourKind = splittedRule[3];

		string[] aRange;
		int a, b;

		for(int i = 0; i < sustains.Length; i++)
        {
			
			if (sustains[i].Contains("-"))
            {
				aRange = sustains[i].Split('-');
				a = Int32.Parse(aRange[0]);
				b = Int32.Parse(aRange[1]);
			
				for(int j = a; j <= b; j++)
                {
					this.rule[j][1] = ALIVE;
                }
            }
            else
            {
				a = Int32.Parse(sustains[i]);
				this.rule[a][1] = ALIVE;
            }
        }

		for (int i = 0; i < births.Length; i++)
		{
			if (births[i].Contains("-"))
			{
				aRange = births[i].Split('-');
				a = Int32.Parse(aRange[0]);
				b = Int32.Parse(aRange[1]);
				for (int j = a; j <= b; j++)
				{
					this.rule[j][0] = ALIVE;
				}
			}
			else
			{
				a = Int32.Parse(births[i]);
				this.rule[a][0] = ALIVE;
			}
		}

		for (int i = 0; i < overpops.Length; i++)
        {
			if (overpops[i].Contains("-"))
            {
				aRange = overpops[i].Split('-');
				a = Int32.Parse(aRange[0]);
				b = Int32.Parse(aRange[1]);
				for (int j = a; j <= b; j++)
                {
					this.rule[j][1] = DEAD;
                }
            }
            else
            {
				a = Int32.Parse(overpops[i]);
				this.rule[a][1] = DEAD;
            }
        }

		if (neighbourKind.Equals("M"))
        {
			this.neighbour = Moore;
        }
        else if (neighbourKind.Equals("VN"))
        {
			this.neighbour = VNeumann;
        }
        else
        {
			throw new System.ArgumentException(neighbourKind + " est inconnu");
        }

		return;
	}

	private int countMooreNeighbours(int x, int y, int z)
	{ //Compte le nombre de voisins vivants selon le voisiange de Moore (un cube)
		int count = 0;
		for (int i = -1; i <= 1; i++)
		{
			for (int j = -1; j <= 1; j++)
			{
				for (int k = -1; k <= 1; k++)
				{
					if (this.torusShape)
					{
						if (!(i == 0 && j == 0 && k == 0))
						{ //Plusieurs opérations modulo pour assurer un résultat positif
							count += this.cellGrid[((x + i) % this.nbrOfCells + this.nbrOfCells) % nbrOfCells,((y + j) % this.nbrOfCells + this.nbrOfCells) % nbrOfCells,((z + k) % this.nbrOfCells + this.nbrOfCells) % nbrOfCells].getStatus();
						}
					}
					else
					{
						if ((x + i >= 0 && x + i < this.nbrOfCells) && (y + j >= 0 && y + j < this.nbrOfCells) && (z + k >= 0 && z + k < this.nbrOfCells)
							&& !(i == 0 && j == 0 && k == 0))
						{
							count += this.cellGrid[x + i,y + j,z + k].getStatus();
						}
					}

				}
			}
		}
		return count;
	}

	private int countVonNeumannNeighbours(int x, int y, int z)
	{ //Compte le nombre de voisins vivants selon le voisinage de Von Neumann (une croix)
		int count = 0;
		count += this.cellGrid[(x + 1) % this.nbrOfCells,y,z].getStatus();
		if (x == 0)
		{
			if (this.torusShape)
			{
				count += this.cellGrid[this.nbrOfCells - 1,y,z].getStatus();
			}
		}

		count += this.cellGrid[x,(y + 1) % this.nbrOfCells,z].getStatus();
		if (y == 0)
		{
			if (this.torusShape)
			{
				count += this.cellGrid[x,this.nbrOfCells - 1,z].getStatus();
			}
		}

		count += this.cellGrid[x,y,(z + 1) % this.nbrOfCells].getStatus();
		if (z == 0)
		{
			if (this.torusShape)
			{
				count += this.cellGrid[x,y,this.nbrOfCells - 1].getStatus();
			}
		}
		return count;
	}

	private void buildNextGrid()
	{
		byte stat, res;
		int count;
		for (int x = 0; x < this.nbrOfCells; x++)
		{
			for (int y = 0; y < this.nbrOfCells; y++)
			{
				for (int z = 0; z < this.nbrOfCells; z++)
				{
					if (this.neighbour == Moore)
                    {
						stat = this.cellGrid[x,y,z].getStatus();
						count = this.countMooreNeighbours(x, y, z);
						res = this.rule[count][stat];

						if (res == ALIVE)
                        {
							this.cellGrid[x, y, z].incrLifespan();
                        }
						else if (res == DEAD)
                        {
							this.cellGrid[x, y, z].resetLifespan();
                        }

						if(stat == ALIVE && res == DEAD)
							this.population--;
                        
						if (stat == DEAD && res == ALIVE)
							this.population++;

						this.nextStatusGrid[x,y,z] = res;
					}
					else if (this.neighbour == VNeumann)
                    {
						stat = this.cellGrid[x, y, z].getStatus();
						count = this.countVonNeumannNeighbours(x, y, z);
						res = this.rule[count][stat];

						if (res == ALIVE)
						{
							this.cellGrid[x, y, z].incrLifespan();
						}
						else if (res == DEAD)
						{
							this.cellGrid[x, y, z].resetLifespan();
						}

						this.nextStatusGrid[x,y,z] = res;
					}
				}
			}
		}
	}

	// Create first Color texture with Positions (XYZ) and other textures with any variables
	void CreateTextures(int size)
	{
		Color[] posColorArray = new Color[size * size * size];
		Color[] statusColorArray = new Color[size * size * size];
		
		positionTexture = new Texture2D(size * size, size, TextureFormat.RGBAFloat, true);
		statusTexture = new Texture2D(size * size, size, TextureFormat.RGBAFloat, true);

		int index;
		
		// Single loop to inject data array values in textures in one shot
		float r = 1.0f / (size - 1.0f);
        for (int x = 0; x < size; x++) {
			for (int y = 0; y < size; y++) {
				for (int z = 0; z < size; z++) {
					index = z + y * size + x * size * size;
					
					// Shape color information for textures1
					// Position
					Color c1 = new Color(r * x, r * y, r * z, 0);
					
					// Status (all deded)
					
					// Just to keep in mind that we store status in Color.r component
					// And lifespan in Color.g
					Color c2 = new Color(DEAD, 0, 0, 0);
					
					posColorArray[index] = c1;
					statusColorArray[index] = c2;
				}
			}
        }
		
		// Inject in textures
		positionTexture.SetPixels(posColorArray);
		statusTexture.SetPixels(statusColorArray);
		positionTexture.Apply();
		statusTexture.Apply();
	}
	
}
