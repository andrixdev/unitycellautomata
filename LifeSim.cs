using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Experimental.VFX;

// Note: GameObject with this script needs to also have a VisualEffect component
public class LifeSim : MonoBehaviour
{
	// Cell Automata mechanics concerns
	private const int DEAD = 0;
	private const int ALIVE = 1;

	private const int Moore = 0;
	private const int VNeumann = 1;

	public readonly int nbrOfCells = 20;
	public float cellLength;
	public bool torusShape;

	private Cell[,,] cellGrid;
	public int[,,] nextStatusGrid;

	private int step = 0;
	public int stepInterval = 20;
	private float timer;
	public float pauseDuration;

	public string ruleKey = "9-20/5-7,12-13,15-16/5/M";
	private string oldRuleKey;
	private int neighbour;
	private int[][] rule;
	
	// Visualization concerns
	private Texture2D positionTexture; // Texture to store positions
    private Texture2D statusTexture; // Texture to store statuses
	private VisualEffect VFX;
	
	// MonoBehaviour Start() called once
	public void Start()
	{
		this.StartAutomaton();
		this.StartVisualsInjection();
	}
	
	// MonoBehaviour Update() called each frame
	public void Update()
	{
		// Update step (quick&dirty way)
		step++;
		if (step % stepInterval == 0)
		{
			// Update cellGrid array
			this.nextStep();
			int midIndex = this.nbrOfCells / 2;
			Debug.Log(this.nextStatusGrid[midIndex-2, midIndex+2, midIndex-3]);
		}
	}
	
	public void StartAutomaton()
	{
		// Init rule array (27x2) -> Change to 27... we can have between 0 and 26 alive neighours, so 27 possibilities right?
		int[] r = new int[] { DEAD, DEAD };
		this.rule = new int[][] { r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r, r }; // 27 values

		// Init cellGrid and nextStatusGrid
		this.cellGrid = new Cell[this.nbrOfCells,this.nbrOfCells,this.nbrOfCells];
		this.nextStatusGrid = new int[this.nbrOfCells,this.nbrOfCells,this.nbrOfCells];
		
		for(int i = 0; i < this.nbrOfCells; i++)
        {
			for(int j = 0; j < this.nbrOfCells; j++)
            {
				for(int k = 0; k < this.nbrOfCells; k++)
                {
					// Cell random initial status
					int initCellStatus = UnityEngine.Random.value < 0.9 ? DEAD : ALIVE;
					this.cellGrid[i,j,k] = new Cell(i, j, k, this.cellLength, initCellStatus);
					
					// Next status grid initial value
					this.nextStatusGrid[i,j,k] = initCellStatus;
                }
            }
        }

		this.translateRule();
	}
	
	public void StartVisualsInjection()
	{
		// Build le textures
        CreateTextures(nbrOfCells);
		
		// Feed le VFX
		VFX = (VisualEffect) GetComponent<VisualEffect>();
		VFX.SetTexture("PositionTexture", positionTexture);
		VFX.SetTexture("StatusTexture", statusTexture);
	}

	// Update both data grid and textures (visuals)
	private void nextStep()
	{
		this.buildNextGrid();
		
		for (int x = 0; x < this.nbrOfCells; x++)
		{
			for (int y = 0; y < this.nbrOfCells; y++)
			{
				for (int z = 0; z < this.nbrOfCells; z++)
				{
					int newCellStatus = this.nextStatusGrid[x,y,z];
					
					// Update automaton status
					this.cellGrid[x,y,z].changeStatus(newCellStatus);
					
					// Update status texture (visuals)
					int[] indexes = new int[2];
					indexes[0] = x + nbrOfCells * y;
					indexes[1] = z;
					statusTexture.SetPixel(indexes[0], indexes[1], new Color(newCellStatus, 0, 0, 0));
				}
			}
		}
		
		// Update textures
		statusTexture.Apply();
	}
	
	private void translateRule()
	{ // Transforme la chaine ruleKey en une rule utilisable
	  // Il y a 26*2 cas (de 0 à 26 voisins vivants, selon son propre status) qui doivent être couverts par une règle au pire
	  // (Il y a plus de voisins dans un voisinage de Moore que dans un voisinage de Von Neumann)
	  // Il faut donc 52 bits pour encoder une règle

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

		for(int i = 0; i < births.Length; i++)
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
				a = Int32.Parse(sustains[i]);
				this.rule[a][0] = ALIVE;
			}
		}

		for(int i = 0; i < overpops.Length; i++)
        {
			if (overpops[i].Contains("-"))
            {
				aRange = overpops[i].Split('-');
				a = Int32.Parse(aRange[0]);
				b = Int32.Parse(aRange[1]);
				for (int j = 0; j <= b; j++)
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
		for (int x = 0; x < this.nbrOfCells; x++)
		{
			for (int y = 0; y < this.nbrOfCells; y++)
			{
				for (int z = 0; z < this.nbrOfCells; z++)
				{
					if (this.neighbour == Moore)
                    {
						int a = this.cellGrid[x,y,z].getStatus();
						int b = this.countMooreNeighbours(x, y, z);
						//Debug.Log("A value (should 0 or 1) -> " + a);
						//Debug.Log("B value (should 0 <= 26) -> " + b);
						int c = this.rule[b][a];
						this.nextStatusGrid[x,y,z] = c;
					}
					else if (this.neighbour == VNeumann)
                    {
						this.nextStatusGrid[x,y,z] = this.rule[this.countVonNeumannNeighbours(x, y, z)][this.cellGrid[x,y,z].getStatus()];
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
		
		// Single loop to inject data array values in textures in one shot
		float r = 1.0f / (size - 1.0f);
        for (int x = 0; x < size; x++) {
			for (int y = 0; y < size; y++) {
				for (int z = 0; z < size; z++) {
					int index = z + y * size + x * size * size;
					
					// Shape color information for textures1
					// Position
					Color c1 = new Color(r * x, r * y, r * z, 0);
					
					// Status (all deded)
					int status = 1;
					Color c2 = new Color(status, 0, 0, 0);// Just to keep in mind that we store status in Color.r component
					
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
